using AgriConnect.Data;
using AgriConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriConnect.Controllers;

[Authorize(Roles = DbSeeder.AdminRole)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Counts = new Dictionary<string, int>
        {
            ["Users"] = await _db.Users.CountAsync(),
            ["Listings"] = await _db.Listings.CountAsync(l => l.Status != ListingStatus.Removed),
            ["Bookings"] = await _db.Bookings.CountAsync(),
            ["Pending bookings"] = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending),
            ["Categories"] = await _db.Categories.CountAsync(),
            ["Reference prices"] = await _db.MarketPrices.CountAsync(),
            ["Open reports"] = await _db.ListingReports.CountAsync(r => !r.Resolved)
        };
        return View();
    }

    public async Task<IActionResult> Users()
    {
        var admins = (await _users.GetUsersInRoleAsync(DbSeeder.AdminRole)).Select(u => u.Id).ToHashSet();

        var users = await _db.Users
            .OrderBy(u => u.Email)
            .Select(u => new AdminUserRow
            {
                Id = u.Id,
                Email = u.Email!,
                FullName = u.FullName,
                District = u.District,
                Listings = _db.Listings.Count(l => l.OwnerId == u.Id && l.Status != ListingStatus.Removed),
                Bookings = _db.Bookings.Count(b => b.RequesterId == u.Id)
            })
            .AsNoTracking()
            .ToListAsync();

        foreach (var row in users) row.IsAdministrator = admins.Contains(row.Id);

        return View(users);
    }

    // --- categories (SOW §2.3.1) ---

    public async Task<IActionResult> Categories() =>
        View(await _db.Categories
            .OrderBy(c => c.ListingType).ThenBy(c => c.Group).ThenBy(c => c.Name)
            .AsNoTracking().ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(
        string name, ListingType listingType, string? group, PriceUnit? suggestedUnit)
    {
        name = name?.Trim() ?? "";
        group = string.IsNullOrWhiteSpace(group) ? null : group.Trim();

        if (name.Length == 0)
            TempData["Error"] = "Enter a category name.";
        else if (await _db.Categories.AnyAsync(c => c.Name == name && c.ListingType == listingType))
            TempData["Error"] = $"\"{name}\" already exists for {listingType}.";
        else
        {
            _db.Categories.Add(new Category
            {
                Name = name,
                ListingType = listingType,
                // Only marketplace products are grouped and have a usual unit; a
                // grouped labour task would render under a heading of one.
                Group = listingType == ListingType.Produce ? group : null,
                SuggestedUnit = listingType == ListingType.Produce ? suggestedUnit : null
            });
            await _db.SaveChangesAsync();
            TempData["Message"] = $"\"{name}\" added.";
        }

        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        // Listings reference categories with DeleteBehavior.Restrict, so refuse here
        // with a readable message rather than letting SQL Server throw.
        if (await _db.Listings.AnyAsync(l => l.CategoryId == id))
            TempData["Error"] = "That category is in use by at least one listing.";
        else
        {
            await _db.Categories.Where(c => c.Id == id).ExecuteDeleteAsync();
            TempData["Message"] = "Category removed.";
        }

        return RedirectToAction(nameof(Categories));
    }

    // --- reported listings (SOW §2.3.1) ---

    public async Task<IActionResult> Reports()
    {
        var reports = await _db.ListingReports
            .Include(r => r.Listing)
            .Include(r => r.ReportedBy)
            .OrderBy(r => r.Resolved)
            .ThenByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return View(reports);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveReport(int id, bool removeListing)
    {
        var report = await _db.ListingReports.Include(r => r.Listing).FirstOrDefaultAsync(r => r.Id == id);
        if (report is null) return NotFound();

        report.Resolved = true;

        if (removeListing && report.Listing is not null)
        {
            report.Listing.Status = ListingStatus.Removed;
            TempData["Message"] = "Report closed and the listing removed.";
        }
        else
        {
            TempData["Message"] = "Report closed, listing left as it is.";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Reports));
    }

    public async Task<IActionResult> Bookings()
    {
        var bookings = await _db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Requester)
            .OrderByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .Take(200)
            .ToListAsync();

        return View(bookings);
    }
}

public class AdminUserRow
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string? FullName { get; set; }
    public string? District { get; set; }
    public int Listings { get; set; }
    public int Bookings { get; set; }
    public bool IsAdministrator { get; set; }
}
