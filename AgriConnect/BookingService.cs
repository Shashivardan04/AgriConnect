using AgriConnect.Data;
using AgriConnect.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriConnect;

public record BookingResult(bool Ok, string? Error = null, Booking? Booking = null)
{
    public static BookingResult Fail(string error) => new(false, error);
    public static BookingResult Success(Booking booking) => new(true, null, booking);
}

/// <summary>
/// Every booking rule lives here. The controller validates input and renders;
/// it does not decide whether a date range is free.
/// </summary>
public class BookingService
{
    private readonly ApplicationDbContext _db;

    public BookingService(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Half-open overlap: [aFrom, aTo) against [bFrom, bTo). A booking ending on the
    /// 10th and one starting on the 10th do NOT overlap - the item is handed over
    /// that morning. Getting this wrong by one day is the classic bug here.
    /// </summary>
    public static bool Overlaps(DateOnly aFrom, DateOnly aTo, DateOnly bFrom, DateOnly bTo)
        => aFrom < bTo && bFrom < aTo;

    public async Task<BookingResult> RequestAsync(
        int listingId, string requesterId, DateOnly from, DateOnly to, string? message)
    {
        var listing = await _db.Listings.FirstOrDefaultAsync(l => l.Id == listingId);
        if (listing is null) return BookingResult.Fail("That listing no longer exists.");
        if (!listing.IsBookable) return BookingResult.Fail("This listing is not booked by date.");
        if (listing.Status != ListingStatus.Active) return BookingResult.Fail("This listing is not available.");
        if (listing.OwnerId == requesterId) return BookingResult.Fail("This is your own listing.");

        if (Validate(from, to) is string problem) return BookingResult.Fail(problem);

        // A pending request may overlap anything - it only has to be free when the
        // owner approves it. But refusing the obviously-taken dates up front saves
        // everyone a round trip.
        var taken = await CompetingBookings(listing)
            .AnyAsync(b => b.Status == BookingStatus.Approved
                           && b.FromDate < to && from < b.ToDate);

        if (taken)
            return BookingResult.Fail(listing.Type == ListingType.Labour
                ? "That worker is already booked for those dates. Try different dates."
                : "Those dates are already booked. Try different dates.");

        var booking = new Booking
        {
            ListingId = listingId,
            RequesterId = requesterId,
            FromDate = from,
            ToDate = to,
            Status = BookingStatus.Pending,
            Message = message
        };

        _db.Bookings.Add(booking);
        Notify(listing.OwnerId, $"New booking request for \"{listing.Title}\".", "/Bookings/Received");
        await _db.SaveChangesAsync();

        return BookingResult.Success(booking);
    }

    /// <summary>Owner marks their own listing unavailable: an approved booking against themselves.</summary>
    public async Task<BookingResult> BlockAsync(int listingId, string ownerId, DateOnly from, DateOnly to, string? reason)
    {
        var listing = await _db.Listings.FirstOrDefaultAsync(l => l.Id == listingId);
        if (listing is null) return BookingResult.Fail("That listing no longer exists.");
        if (listing.OwnerId != ownerId) return BookingResult.Fail("Only the owner can block dates.");
        if (Validate(from, to) is string problem) return BookingResult.Fail(problem);

        var block = new Booking
        {
            ListingId = listingId,
            RequesterId = ownerId,
            FromDate = from,
            ToDate = to,
            Status = BookingStatus.Pending,
            Message = reason ?? "Unavailable"
        };

        _db.Bookings.Add(block);
        await _db.SaveChangesAsync();

        // Goes through the same approval path, so a block cannot double-book either.
        var approved = await ApproveAsync(block.Id, ownerId);

        if (!approved.Ok)
        {
            // The block clashed with a confirmed booking. Drop the row rather than
            // leaving a pending request the owner never made showing in their list.
            _db.Bookings.Remove(block);
            await _db.SaveChangesAsync();
        }

        return approved;
    }

    /// <summary>
    /// Approve a pending request. This is the only place that can create a confirmed
    /// booking, and the only place where two users can race each other.
    /// </summary>
    public async Task<BookingResult> ApproveAsync(int bookingId, string actingUserId, bool isAdmin = false)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        // Serialise approvals for the resource being reserved. Without it, two owners
        // clicking Approve at the same instant both read "no overlap" and both commit.
        // sp_getapplock cannot deadlock and releases when the transaction ends.
        //
        // The key is the RESOURCE, not the listing. Two tasks of the same worker would
        // otherwise take two different locks, both see a free calendar and both commit,
        // which is the very race this lock exists to close.
        // BookingResourceKey is computed in C#, so the listing has to be materialised
        // before it can be read - it cannot be projected into SQL.
        var target = await _db.Bookings.AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => b.Listing)
            .FirstOrDefaultAsync();

        if (target is null) return BookingResult.Fail("That booking no longer exists.");

        if (_db.Database.IsSqlServer())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000",
                target.BookingResourceKey);
        }

        var booking = await _db.Bookings
            .Include(b => b.Listing).ThenInclude(l => l!.Category)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking?.Listing is null) return BookingResult.Fail("That booking no longer exists.");
        if (booking.Listing.OwnerId != actingUserId && !isAdmin)
            return BookingResult.Fail("Only the owner can approve this request.");
        if (booking.Status != BookingStatus.Pending)
            return BookingResult.Fail($"This request is already {booking.Status.ToString().ToLowerInvariant()}.");

        // Re-check INSIDE the lock. The check outside it proves nothing.
        var clash = await CompetingBookings(booking.Listing)
            .AnyAsync(b => b.Id != booking.Id
                           && b.Status == BookingStatus.Approved
                           && b.FromDate < booking.ToDate && booking.FromDate < b.ToDate);

        if (clash)
            return BookingResult.Fail(booking.Listing.Type == ListingType.Labour
                ? "That worker was already booked for those dates."
                : "Those dates were taken by another confirmed booking.");

        booking.Status = BookingStatus.Approved;

        // Anything still pending that overlaps the dates just confirmed cannot be
        // granted, so reject it now rather than leaving it to rot. For a worker this
        // spans every task they offer, not only the one just booked.
        var superseded = await CompetingBookings(booking.Listing)
            .Where(b => b.Id != booking.Id
                        && b.Status == BookingStatus.Pending
                        && b.FromDate < booking.ToDate && booking.FromDate < b.ToDate)
            .ToListAsync();

        foreach (var other in superseded)
        {
            other.Status = BookingStatus.Rejected;
            Notify(other.RequesterId,
                $"Your request for \"{booking.Listing.Title}\" was declined - those dates were booked.",
                "/Bookings/Mine");
        }

        if (booking.RequesterId != booking.Listing.OwnerId)
            Notify(booking.RequesterId,
                $"Your booking for \"{booking.Listing.Title}\" was approved.", "/Bookings/Mine");

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return BookingResult.Success(booking);
    }

    public async Task<BookingResult> SetStatusAsync(
        int bookingId, string actingUserId, BookingStatus target, bool isAdmin = false)
    {
        var booking = await _db.Bookings.Include(b => b.Listing)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking?.Listing is null) return BookingResult.Fail("That booking no longer exists.");

        var isOwner = booking.Listing.OwnerId == actingUserId || isAdmin;
        var isRequester = booking.RequesterId == actingUserId;

        var allowed = target switch
        {
            // The owner decides on requests; the requester may withdraw their own.
            BookingStatus.Rejected => isOwner && booking.Status == BookingStatus.Pending,
            BookingStatus.Cancelled => (isRequester || isOwner)
                                       && booking.Status is BookingStatus.Pending or BookingStatus.Approved,
            BookingStatus.Completed => isOwner && booking.Status == BookingStatus.Approved,
            _ => false
        };

        if (!allowed) return BookingResult.Fail("That change is not allowed from here.");

        booking.Status = target;

        var who = isRequester ? booking.Listing.OwnerId : booking.RequesterId;
        var verb = target.ToString().ToLowerInvariant();
        Notify(who, $"Booking for \"{booking.Listing.Title}\" was {verb}.", "/Bookings/Mine");

        await _db.SaveChangesAsync();
        return BookingResult.Success(booking);
    }

    /// <summary>
    /// Confirmed dates for the availability calendar. For a labour listing this is the
    /// worker's whole schedule, so every one of their task pages shows the same blocked
    /// days and the shared calendar is visible wherever you look at them.
    /// </summary>
    public async Task<List<(DateOnly From, DateOnly To)>> BookedRangesAsync(int listingId)
    {
        var listing = await _db.Listings.AsNoTracking().FirstOrDefaultAsync(l => l.Id == listingId);
        if (listing is null) return [];

        var rows = await CompetingBookings(listing)
            .Where(b => b.Status == BookingStatus.Approved
                        && b.ToDate >= DateOnly.FromDateTime(DateTime.Today))
            .Select(b => new { b.FromDate, b.ToDate })
            .AsNoTracking()
            .ToListAsync();

        return rows.Select(r => (r.FromDate, r.ToDate)).ToList();
    }

    /// <summary>
    /// Bookings that compete with this one. For labour that is every task the same
    /// worker offers, because the person is what gets reserved; for equipment and
    /// produce it is only this listing, since each machine is its own resource.
    /// </summary>
    private IQueryable<Booking> CompetingBookings(Listing listing) =>
        listing.Type == ListingType.Labour
            ? _db.Bookings.Where(b => b.Listing!.Type == ListingType.Labour
                                      && b.Listing.OwnerId == listing.OwnerId)
            : _db.Bookings.Where(b => b.ListingId == listing.Id);

    private static string? Validate(DateOnly from, DateOnly to)
    {
        if (to <= from) return "The end date must be after the start date.";
        if (from < DateOnly.FromDateTime(DateTime.Today)) return "Dates cannot start in the past.";
        if (from.AddYears(1) < to) return "A booking cannot run longer than a year.";
        return null;
    }

    private void Notify(string userId, string message, string link) =>
        _db.Notifications.Add(new Notification { UserId = userId, Message = message, LinkUrl = link });
}
