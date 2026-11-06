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
    /// <summary>Matches the length of the Message column a note is stored in.</summary>
    public const int MaxNoteLength = 500;

    /// <summary>
    /// The smallest orderable amount. Matches the scale of the decimal(10,2) column:
    /// anything under this rounds to zero on the way in, which is not an order.
    /// </summary>
    public const decimal MinOrderQuantity = 0.01m;

    /// <summary>
    /// The longest a standing order may run. Matches the ceiling Validate already puts
    /// on a date range, so a subscription cannot outlive what the rest of the service
    /// is willing to reason about.
    /// </summary>
    public const int MaxOrderDays = 365;

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

        if (Validate(from, to, message) is string problem) return BookingResult.Fail(problem);

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

    /// <summary>
    /// Order part of a produce listing. The buyer names a collection day and how much
    /// they want; from there it is an ordinary request the seller accepts or declines.
    ///
    /// <paramref name="days"/> is 1 for a single collection and more for a standing
    /// order, which only products an administrator has marked as sold daily accept.
    /// <paramref name="quantity"/> is per day either way.
    /// </summary>
    public async Task<BookingResult> OrderAsync(
        int listingId, string buyerId, DateOnly pickup, decimal quantity, string? message, int days = 1)
    {
        // The category comes with it. Whether this is a daily product is read through
        // the category, and a missing one would quietly make milk behave like onions.
        var listing = await _db.Listings
            .Include(l => l.Category)
            .FirstOrDefaultAsync(l => l.Id == listingId);

        if (listing is null) return BookingResult.Fail("That listing no longer exists.");
        if (!listing.IsOrderable) return BookingResult.Fail("This listing is booked by date, not ordered by quantity.");
        if (listing.Status != ListingStatus.Active) return BookingResult.Fail("This listing is not available.");
        if (listing.OwnerId == buyerId) return BookingResult.Fail("This is your own listing.");

        if (days < 1 || days > MaxOrderDays)
            return BookingResult.Fail("A standing order runs for at least a day and at most a year.");

        // Taking some every day only means anything where there is some every day.
        // Against a fixed stock, "20 kg a day for five days" is ambiguous rather than
        // generous, so it is refused instead of guessed at.
        if (days > 1 && !listing.IsDailySale)
            return BookingResult.Fail("This one is not sold by the day. Order what you need in one go.");

        // The stored range is the collection days, [pickup, pickup + days), so Validate
        // applies unchanged: nothing collected in the past, same ceiling on the note.
        var to = pickup.AddDays(days);
        if (Validate(pickup, to, message) is string problem) return BookingResult.Fail(problem);

        // Quantity arrives as a loose action parameter, so the [Range] on the column
        // never fires. Round first: the column is decimal(10,2), and an unrounded
        // figure would be checked here and then stored as something else.
        quantity = Math.Round(quantity, 2);
        if (quantity < MinOrderQuantity) return BookingResult.Fail("Enter how much you want.");

        // Livestock is counted, not weighed. decimal(10,2) would store an order for
        // two and a half goats perfectly happily.
        if (listing.PriceUnit == PriceUnit.PerHead && quantity != decimal.Truncate(quantity))
            return BookingResult.Fail("Livestock is sold by the head, so enter a whole number.");

        // Like the taken-dates check in RequestAsync this proves nothing - the seller
        // may approve somebody else first. It spares an obviously doomed order a round
        // trip; the check that counts runs inside the lock in ApproveAsync.
        if (await TooMuchAsync(listing, pickup, to, quantity, null) is string tooMuch)
            return BookingResult.Fail(tooMuch);

        var booking = new Booking
        {
            ListingId = listingId,
            RequesterId = buyerId,
            FromDate = pickup,
            ToDate = to,
            Quantity = quantity,
            UnitPrice = listing.Price,
            Status = BookingStatus.Pending,
            Message = message
        };

        _db.Bookings.Add(booking);
        Notify(listing.OwnerId, $"New order for \"{listing.Title}\".", "/Bookings/Received");
        await _db.SaveChangesAsync();

        return BookingResult.Success(booking);
    }

    /// <summary>Owner marks their own listing unavailable: an approved booking against themselves.</summary>
    public async Task<BookingResult> BlockAsync(int listingId, string ownerId, DateOnly from, DateOnly to, string? reason)
    {
        var listing = await _db.Listings.FirstOrDefaultAsync(l => l.Id == listingId);
        if (listing is null) return BookingResult.Fail("That listing no longer exists.");
        if (listing.OwnerId != ownerId) return BookingResult.Fail("Only the owner can block dates.");
        // Blocking reserves a period, which only means something for a thing reserved by
        // period. Without this an owner could POST a produce id and get an approved
        // booking carrying no quantity - a row the drawdown would then have to defend
        // against for the rest of time.
        if (!listing.IsBookable) return BookingResult.Fail("This listing is not booked by date.");
        if (Validate(from, to, reason) is string problem) return BookingResult.Fail(problem);

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

        await LockResourceAsync(target);

        var booking = await _db.Bookings
            .Include(b => b.Listing).ThenInclude(l => l!.Category)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking?.Listing is null) return BookingResult.Fail("That booking no longer exists.");
        if (booking.Listing.OwnerId != actingUserId && !isAdmin)
            return BookingResult.Fail("Only the owner can approve this request.");
        if (booking.Status != BookingStatus.Pending)
            return BookingResult.Fail($"This request is already {booking.Status.ToString().ToLowerInvariant()}.");

        // Re-check INSIDE the lock. The check outside it proves nothing.
        //
        // Produce and the other two ask different questions here, and that is the whole
        // of the difference between them: a tractor asks "are these dates free?", a sack
        // of tomatoes asks "is there enough left?". Two buyers taking 20 kg each of the
        // same 100 kg do not compete, where two renters wanting the same Tuesday do.
        var refusal = await CanApproveAsync(booking);

        if (refusal is not null) return BookingResult.Fail(refusal);

        booking.Status = BookingStatus.Approved;

        // Anything still pending that this approval has just made impossible is declined
        // now rather than left to rot. For a date booking that is everything overlapping
        // it - for a worker, across every task they offer. For an order it is nothing at
        // all unless the stock ran out, because the next buyer may still be served.
        foreach (var other in await SupersededAsync(booking))
        {
            other.Status = BookingStatus.Rejected;
            Notify(other.RequesterId,
                booking.Listing.IsOrderable
                    ? $"Your order for \"{booking.Listing.Title}\" was declined - it sold out."
                    : $"Your request for \"{booking.Listing.Title}\" was declined - those dates were booked.",
                "/Bookings/Mine");
        }

        if (booking.RequesterId != booking.Listing.OwnerId)
            Notify(booking.RequesterId,
                booking.Listing.IsOrderable
                    ? $"Your order for \"{booking.Listing.Title}\" was accepted."
                    : $"Your booking for \"{booking.Listing.Title}\" was approved.",
                "/Bookings/Mine");

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return BookingResult.Success(booking);
    }

    public async Task<BookingResult> SetStatusAsync(
        int bookingId, string actingUserId, BookingStatus target, bool isAdmin = false)
    {
        // Category included: whether cancelling has to give stock back depends on
        // whether this listing depletes, and that is read through the category.
        var booking = await _db.Bookings
            .Include(b => b.Listing).ThenInclude(l => l!.Category)
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

        // Cancelling a confirmed order puts what it took back on the shelf. That is a
        // read-modify-write on Listing.Quantity, so it needs the same serialisation
        // approval uses: two members cancelling at the same instant would otherwise both
        // read the old figure and the second write would swallow the first restock.
        // Rejected only ever comes from Pending, which never took anything, and
        // Completed means the produce changed hands - neither of those restocks.
        // A daily listing was never drawn down, so there is nothing to give back - its
        // capacity frees itself the moment the row stops counting as approved.
        var restocks = target == BookingStatus.Cancelled
                       && booking.Status == BookingStatus.Approved
                       && booking.Listing.IsOrderable
                       && !booking.Listing.IsDailySale
                       && booking.Quantity is > 0;

        // Unconditional: SaveChanges opens one of these anyway, and one path beats a
        // nullable transaction threaded through the rest of the method.
        await using var tx = await _db.Database.BeginTransactionAsync();

        if (restocks)
        {
            await LockResourceAsync(booking.Listing);

            // The listing was read before the lock was taken, so what it holds now is
            // whatever an approval may have committed in the meantime.
            await _db.Entry(booking.Listing).ReloadAsync();

            booking.Listing.Quantity = (booking.Listing.Quantity ?? 0) + booking.Quantity!.Value;

            // Back on sale. Without this the listing stays Sold with stock sitting in
            // it, invisible to the very search that would have sold it.
            if (booking.Listing.Status == ListingStatus.Sold)
                booking.Listing.Status = ListingStatus.Active;
        }

        booking.Status = target;

        // Tell the other party. An owner block is the owner's own request against
        // their own listing, so there is nobody else to tell and the notification
        // would only be the owner informing themselves.
        var who = booking.RequesterId == booking.Listing.OwnerId
            ? null
            : isRequester ? booking.Listing.OwnerId : booking.RequesterId;

        var verb = target.ToString().ToLowerInvariant();
        if (who is not null)
            Notify(who, booking.Listing.IsOrderable
                ? $"Order for \"{booking.Listing.Title}\" was {verb}."
                : $"Booking for \"{booking.Listing.Title}\" was {verb}.", "/Bookings/Mine");

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
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

        // An order's range is the collection day, not a period the listing is taken for.
        // Reporting those days would tell a buyer the tomatoes are unavailable on the one
        // day somebody happens to be collecting some. This endpoint is anonymous and
        // takes any id, so the guard belongs here and not in the view that omits it.
        if (listing.IsOrderable) return [];

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

    /// <summary>
    /// Whether this approval can go ahead, run inside the caller's lock, and what to
    /// say if it cannot. Three kinds of thing, three questions: a date range is held
    /// exclusively, a sack of tomatoes runs down, a cow has milked again by morning.
    ///
    /// For a depleting listing this is also where the stock is taken out, because the
    /// decision and the subtraction have to happen under the same lock.
    /// </summary>
    private async Task<string?> CanApproveAsync(Booking booking)
    {
        var listing = booking.Listing!;

        if (!listing.IsOrderable) return await DatesStillFreeAsync(booking);

        // An order with no quantity can only arrive through a route that should not
        // exist. Refuse it rather than quietly reading it as an order for nothing.
        if (booking.Quantity is not decimal wanted || wanted <= 0)
            return "That order carries no quantity.";

        if (await TooMuchAsync(listing, booking.FromDate, booking.ToDate, wanted, booking.Id) is string refusal)
            return refusal;

        // A daily listing is never drawn down and so never sells out: what was promised
        // for next week says nothing about the week after.
        if (listing.IsDailySale) return null;

        listing.Quantity = (listing.Quantity ?? 0) - wanted;

        // Nothing left to sell. The seller no longer has to remember to flip this by
        // hand, which is what used to leave sold-out produce sitting in the results.
        if (listing.Quantity <= 0) listing.Status = ListingStatus.Sold;

        return null;
    }

    /// <summary>
    /// Whether this much, over these days, is more than the listing can serve - and the
    /// reason if it is. A depleting listing is compared against what is left; a daily
    /// one against the most already promised on any single day of the range.
    /// </summary>
    private async Task<string?> TooMuchAsync(
        Listing listing, DateOnly from, DateOnly to, decimal wanted, int? excludeBookingId)
    {
        var capacity = listing.Quantity ?? 0;

        if (!listing.IsDailySale)
            return wanted > capacity ? SoldOutMessage(listing, capacity) : null;

        var noun = ListingSlug.QuantityLabel(listing.PriceUnit);

        if (wanted > capacity)
            return $"There is only {capacity:0.##} {noun} a day to be had.";

        var committed = await _db.Bookings
            .Where(b => b.ListingId == listing.Id
                        && b.Status == BookingStatus.Approved
                        && b.Quantity != null
                        && b.FromDate < to && from < b.ToDate)
            .Select(b => new { b.Id, b.FromDate, b.ToDate, b.Quantity })
            .ToListAsync();

        if (excludeBookingId is int skip) committed.RemoveAll(b => b.Id == skip);
        if (committed.Count == 0) return null;

        // What is promised on a given day only ever RISES on a day some order starts,
        // so the busiest day in the range is either its first day or one of those.
        // Checking those few beats walking every date of a year-long range.
        //
        // ponytail: O(n^2) across one listing's overlapping orders, bounded by the
        // one-year cap in Validate. A running-total sweep if that stops being a handful.
        var candidates = committed
            .Select(b => b.FromDate)
            .Where(day => day > from)
            .Append(from)
            .Distinct();

        foreach (var day in candidates)
        {
            var promised = committed
                .Where(b => b.FromDate <= day && day < b.ToDate)
                .Sum(b => b.Quantity ?? 0);

            if (promised + wanted > capacity)
                return $"Only {capacity - promised:0.##} {noun} a day is free from {day:d MMM yyyy}.";
        }

        return null;
    }

    /// <summary>The date half of approval: the overlap re-check, inside the lock.</summary>
    private async Task<string?> DatesStillFreeAsync(Booking booking)
    {
        var clash = await CompetingBookings(booking.Listing!)
            .AnyAsync(b => b.Id != booking.Id
                           && b.Status == BookingStatus.Approved
                           && b.FromDate < booking.ToDate && booking.FromDate < b.ToDate);

        if (!clash) return null;

        return booking.Listing!.Type == ListingType.Labour
            ? "That worker was already booked for those dates."
            : "Those dates were taken by another confirmed booking.";
    }

    /// <summary>
    /// Pending requests this approval has just made impossible. For a date booking that
    /// is everything overlapping it; for an order it is nothing at all unless the stock
    /// ran out, because a smaller order behind it can still be served.
    /// </summary>
    private async Task<List<Booking>> SupersededAsync(Booking booking)
    {
        var listing = booking.Listing!;

        if (listing.IsOrderable)
            return listing.Status != ListingStatus.Sold
                ? []
                : await _db.Bookings
                    .Where(b => b.ListingId == listing.Id
                                && b.Id != booking.Id
                                && b.Status == BookingStatus.Pending)
                    .ToListAsync();

        return await CompetingBookings(listing)
            .Where(b => b.Id != booking.Id
                        && b.Status == BookingStatus.Pending
                        && b.FromDate < booking.ToDate && booking.FromDate < b.ToDate)
            .ToListAsync();
    }

    /// <summary>
    /// Serialise everything that touches one reservable resource. sp_getapplock cannot
    /// deadlock and releases when the transaction ends, so it needs an open transaction
    /// to be worth anything. SQL Server only; SQLite serialises its writers by itself.
    /// </summary>
    private async Task LockResourceAsync(Listing listing)
    {
        if (!_db.Database.IsSqlServer()) return;

        await _db.Database.ExecuteSqlRawAsync(
            "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000",
            listing.BookingResourceKey);
    }

    /// <summary>
    /// "0.##" rather than "N0": a quarter of a quintal is a real amount, and N0 would
    /// report it as nothing left while there is still some.
    /// </summary>
    private static string SoldOutMessage(Listing listing, decimal left) =>
        left <= 0
            ? "That listing has sold out."
            : $"Only {left:0.##} {ListingSlug.QuantityLabel(listing.PriceUnit)} is left.";

    private static string? Validate(DateOnly from, DateOnly to, string? note)
    {
        if (to <= from) return "The end date must be after the start date.";
        if (from < DateOnly.FromDateTime(DateTime.Today)) return "Dates cannot start in the past.";
        if (from.AddYears(1) < to) return "A booking cannot run longer than a year.";

        // Booking.Message is a 500-character column and EF does not enforce the
        // annotation, so without this the insert reaches SQL Server, is refused for
        // truncation, and the member gets a 500 instead of being told to shorten it.
        if (note is not null && note.Length > MaxNoteLength) return "Keep it under 500 characters.";

        return null;
    }

    private void Notify(string userId, string message, string link) =>
        _db.Notifications.Add(new Notification { UserId = userId, Message = message, LinkUrl = link });
}
