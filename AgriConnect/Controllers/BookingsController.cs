using AgriConnect.Data;
using AgriConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriConnect.Controllers;

[Authorize]
public class BookingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly BookingService _bookings;
    private readonly UserManager<ApplicationUser> _users;

    public BookingsController(ApplicationDbContext db, BookingService bookings, UserManager<ApplicationUser> users)
    {
        _db = db;
        _bookings = bookings;
        _users = users;
    }

    /// <summary>Requests I have made.</summary>
    public async Task<IActionResult> Mine()
    {
        var me = _users.GetUserId(User)!;
        var bookings = await _db.Bookings
            .Where(b => b.RequesterId == me)
            .Include(b => b.Listing).ThenInclude(l => l!.Owner)
            .OrderByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        ViewBag.Heading = "My booking requests";
        ViewBag.Perspective = "requester";
        return View("List", bookings);
    }

    /// <summary>Requests other people have made on my listings.</summary>
    public async Task<IActionResult> Received()
    {
        var me = _users.GetUserId(User)!;
        var bookings = await _db.Bookings
            .Where(b => b.Listing!.OwnerId == me)
            .Include(b => b.Listing)
            .Include(b => b.Requester)
            .OrderBy(b => b.Status == BookingStatus.Pending ? 0 : 1)
            .ThenByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        ViewBag.Heading = "Requests for my listings";
        ViewBag.Perspective = "owner";
        return View("List", bookings);
    }

    // Not named Request: that would shadow ControllerBase.Request and quietly break
    // any use of Request.Path inside this controller.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestDates(int listingId, DateOnly from, DateOnly to, string? message)
    {
        var result = await _bookings.RequestAsync(listingId, _users.GetUserId(User)!, from, to, message);

        if (result.Ok) TempData["Message"] = "Request sent. The owner will accept or decline it.";
        else TempData["Error"] = result.Error;

        return RedirectToListing(listingId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(int listingId, DateOnly from, DateOnly to, string? reason)
    {
        var result = await _bookings.BlockAsync(listingId, _users.GetUserId(User)!, from, to, reason);

        if (result.Ok) TempData["Message"] = "Those dates are now marked unavailable.";
        else TempData["Error"] = result.Error;

        return RedirectToListing(listingId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var result = await _bookings.ApproveAsync(id, _users.GetUserId(User)!, User.IsInRole(DbSeeder.AdminRole));

        if (result.Ok) TempData["Message"] = "Booking confirmed. Any clashing requests were declined.";
        else TempData["Error"] = result.Error;

        return RedirectToAction(nameof(Received));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, BookingStatus status, string? back)
    {
        var result = await _bookings.SetStatusAsync(
            id, _users.GetUserId(User)!, status, User.IsInRole(DbSeeder.AdminRole));

        if (result.Ok) TempData["Message"] = $"Booking {status.ToString().ToLowerInvariant()}.";
        else TempData["Error"] = result.Error;

        return RedirectToAction(back == "mine" ? nameof(Mine) : nameof(Received));
    }

    /// <summary>Confirmed dates for the availability calendar.</summary>
    [AllowAnonymous, HttpGet("bookings/availability/{listingId:int}")]
    public async Task<IActionResult> Availability(int listingId)
    {
        var ranges = await _bookings.BookedRangesAsync(listingId);

        // Expanded to individual days: the calendar just asks "is this day taken?"
        var days = ranges
            .SelectMany(r => Enumerable
                .Range(0, r.To.DayNumber - r.From.DayNumber)
                .Select(offset => r.From.AddDays(offset)))
            .Distinct()
            .Select(d => d.ToString("yyyy-MM-dd"));

        return Json(days);
    }

    private IActionResult RedirectToListing(int listingId)
    {
        var type = _db.Listings.Where(l => l.Id == listingId).Select(l => l.Type).FirstOrDefault();
        return RedirectToAction("Details", "Listings",
            new { type = ListingSlug.From(type), id = listingId });
    }
}
