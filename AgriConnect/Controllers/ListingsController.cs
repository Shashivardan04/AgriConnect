using AgriConnect.Data;
using AgriConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace AgriConnect.Controllers;

[Route("{type:regex(" + ListingSlug.RouteConstraint + ")}")]
public class ListingsController : Controller
{
    private const long MaxImageBytes = 4 * 1024 * 1024;
    private const int MaxImagesPerListing = 5;
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IWebHostEnvironment _env;

    // Dropdown option text is built here rather than in the view, so the controller
    // needs the plain string localizer. Same resource file the views use.
    private readonly IStringLocalizer<SharedResource> _text;

    public ListingsController(ApplicationDbContext db, UserManager<ApplicationUser> users,
                              IWebHostEnvironment env, IStringLocalizer<SharedResource> text)
    {
        _db = db;
        _users = users;
        _env = env;
        _text = text;
    }

    /// <summary>
    /// Every action here reads the section from the route, where a constraint already
    /// limits it to the three known slugs. Model binding can still hand the action a
    /// null: if MVC fails to read the request body it records the error and binds
    /// every parameter to its default, and a GET carrying a multipart/form-data
    /// content type and no body is enough to cause that. ToType(null) then threw and
    /// every page in this controller answered 500 to one extra header.
    ///
    /// So take the slug from the route, which never passes through the body.
    /// </summary>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;

        // A request whose body cannot be read never reaches the action with usable
        // arguments. MVC records the failure and leaves every parameter out of
        // ActionArguments, so the action runs with `type` null and `filter` null and
        // falls over on whichever it touches first. One extra header on an ordinary
        // GET - a multipart content type with no body behind it - was enough to do
        // that, and every page in this controller answered 500 to it.
        //
        // That is a malformed request, not a page. Refuse it once here rather than
        // teach seven actions to survive arguments that were never bound.
        if (request.HasFormContentType)
        {
            try
            {
                _ = request.Form;
            }
            catch (Exception e) when (e is InvalidDataException or IOException)
            {
                context.Result = BadRequest();
                return;
            }
        }

        // Read the section from the route rather than the bound parameter: the route
        // constraint has already limited it to the three known slugs, and a route
        // value never passes through the body.
        var slug = context.RouteData.Values["type"] as string;

        if (!ListingSlug.IsKnown(slug))
        {
            context.Result = NotFound();
            return;
        }

        context.ActionArguments["type"] = slug;

        base.OnActionExecuting(context);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string type, ListingSearch filter)
    {
        var listingType = ListingSlug.ToType(type);

        // Query string in, database query out: clamp it before anything reads it.
        filter = filter.Normalised();

        // Every section is browsed category-first: pick the machine, product or task
        // you want, then compare what is on offer. The flat grid is what you get once
        // a category is chosen, or whenever a filter is applied.
        if (filter.CategoryId is null && !filter.AnyFilter)
            return await CategoryIndex(listingType, type);

        var q = _db.Listings
            .Where(l => l.Type == listingType && l.Status != ListingStatus.Removed);

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var term = filter.Q.Trim();
            q = q.Where(l => l.Title.Contains(term)
                             || (l.Description != null && l.Description.Contains(term))
                             || (l.Skills != null && l.Skills.Contains(term)));
        }

        if (filter.CategoryId is int categoryId)
            q = q.Where(l => l.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(filter.District))
            q = q.Where(l => l.District == filter.District);

        if (!string.IsNullOrWhiteSpace(filter.Place))
        {
            var place = filter.Place.Trim();
            q = q.Where(l => (l.Town != null && l.Town.Contains(place))
                             || (l.Village != null && l.Village.Contains(place)));
        }

        if (filter.MinPrice is decimal min) q = q.Where(l => l.Price >= min);
        if (filter.MaxPrice is decimal max) q = q.Where(l => l.Price <= max);

        // Gender lives on the worker, not the task entry, so this reads through the
        // owner. Only meaningful for labour; equipment and produce ignore it.
        if (listingType == ListingType.Labour && filter.Gender is Gender gender)
            q = q.Where(l => l.Owner!.Gender == gender);

        // Availability: drop anything already approved over any part of the wanted
        // range. Same half-open comparison the booking rule uses, so search and
        // approval can never disagree about what "free" means - otherwise a worker
        // shows as available and then refuses the booking.
        if (filter.From is DateOnly from && filter.To is DateOnly to && to > from)
        {
            if (listingType == ListingType.Labour)
            {
                // A booked worker is unavailable for every task they offer, not just
                // the one that was booked.
                q = q.Where(l => !_db.Bookings.Any(b =>
                    b.Listing!.Type == ListingType.Labour
                    && b.Listing.OwnerId == l.OwnerId
                    && b.Status == BookingStatus.Approved
                    && b.FromDate < to && from < b.ToDate));
            }
            else
            {
                q = q.Where(l => !l.Bookings.Any(b =>
                    b.Status == BookingStatus.Approved && b.FromDate < to && from < b.ToDate));
            }
        }

        q = filter.Sort switch
        {
            "price" => q.OrderBy(l => l.Price),
            "price_desc" => q.OrderByDescending(l => l.Price),
            "title" => q.OrderBy(l => l.Title),
            "experience" => q.OrderByDescending(l => l.ExperienceYears),
            // Labour defaults to best match: nearest first, then cheapest, then most
            // experienced. "Nearest" is district then town - the schema holds place
            // names, not coordinates, so this is as near as proximity gets here.
            _ when listingType == ListingType.Labour => BestMatch(q, filter),
            _ => q.OrderByDescending(l => l.CreatedAt)
        };

        var listings = await q
            .Include(l => l.Category)
            .Include(l => l.Images)
            .AsNoTracking()
            .Take(100)   // ponytail: no paging until a result set actually passes 100
            .ToListAsync();

        await PopulateForm(listingType);
        ViewBag.Slug = type;
        ViewBag.ListingType = listingType;
        ViewBag.Filter = filter;

        // The chosen category names the heading and the breadcrumb; labour
        // additionally leads with the person rather than the listing.
        ViewBag.Category = filter.CategoryId is int id
            ? await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id)
            : null;

        if (listingType == ListingType.Labour)
        {
            ViewBag.NearDistrict = NearDistrict(filter);
            ViewBag.Owners = await OwnerLookupAsync(listings);
        }

        return View(listings);
    }

    /// <summary>
    /// The landing page for labour and the marketplace: the tasks or products
    /// themselves, each with how many people are offering it and from what price.
    /// </summary>
    private async Task<IActionResult> CategoryIndex(ListingType listingType, string slug)
    {
        var categories = await _db.Categories
            .Where(c => c.ListingType == listingType)
            .AsNoTracking()
            .ToListAsync();

        // ponytail: pulled and aggregated in memory. A nested GROUP BY in SQL is far
        // more code than it saves here; move to one if active listings pass a few thousand.
        var facts = await _db.Listings
            .Where(l => l.Type == listingType && l.Status == ListingStatus.Active)
            .Select(l => new CategoryIndexRow.Facts(
                l.CategoryId, l.OwnerId, l.PriceUnit, l.Price, l.Quantity))
            .AsNoTracking()
            .ToListAsync();

        var rows = CategoryIndexRow.Build(categories, facts);

        ViewBag.Slug = slug;
        ViewBag.ListingType = listingType;
        return View("CategoryIndex", rows);
    }

    /// <summary>
    /// One worker: every task they have registered for with its own wage, and the single
    /// calendar they share across all of them.
    /// </summary>
    [HttpGet("worker/{workerId}")]
    public async Task<IActionResult> Worker(string type, string workerId)
    {
        // Workers are a labour idea; /equipment/worker/x is not a page.
        if (ListingSlug.ToType(type) != ListingType.Labour) return NotFound();

        var tasks = await _db.Listings
            .Where(l => l.Type == ListingType.Labour && l.OwnerId == workerId
                        && l.Status != ListingStatus.Removed)
            .Include(l => l.Category)
            .Include(l => l.Images)
            .OrderBy(l => l.Category!.Name)
            .AsNoTracking()
            .ToListAsync();

        if (tasks.Count == 0) return NotFound();

        var worker = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == workerId);
        if (worker is null) return NotFound();

        ViewBag.Slug = type;
        ViewBag.Worker = worker;
        ViewBag.IsSelf = _users.GetUserId(User) == workerId;
        return View(tasks);
    }

    /// <summary>
    /// Nearest first, then cheapest, then most experienced. The district the farmer
    /// cares about is the one they filtered on, or failing that their own.
    /// </summary>
    private IOrderedQueryable<Listing> BestMatch(IQueryable<Listing> q, ListingSearch filter)
    {
        var district = NearDistrict(filter);
        var town = string.IsNullOrWhiteSpace(filter.Place) ? null : filter.Place.Trim();

        return q
            .OrderBy(l => district != null && l.District == district ? 0 : 1)
            .ThenBy(l => town != null && l.Town == town ? 0 : 1)
            .ThenBy(l => l.Price)
            .ThenByDescending(l => l.ExperienceYears);
    }

    private string? NearDistrict(ListingSearch filter) =>
        !string.IsNullOrWhiteSpace(filter.District)
            ? filter.District.Trim()
            : User.Identity?.IsAuthenticated == true
                ? _db.Users.Where(u => u.Id == _users.GetUserId(User))
                      .Select(u => u.District).FirstOrDefault()
                : null;

    /// <summary>Owner details for the labour cards, which lead with the person.</summary>
    private async Task<Dictionary<string, ApplicationUser>> OwnerLookupAsync(List<Listing> listings)
    {
        var ids = listings.Select(l => l.OwnerId).Distinct().ToList();
        return await _db.Users.Where(u => ids.Contains(u.Id)).AsNoTracking()
            .ToDictionaryAsync(u => u.Id);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(string type, int id)
    {
        var listing = await _db.Listings
            .Include(l => l.Category)
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id && l.Type == ListingSlug.ToType(type));

        if (listing is null) return NotFound();

        // SOW §2.3.7: show the administrator's reference price beside the asking price.
        // Prefer the same district; fall back to any district for that category, since a
        // figure from the next district over still beats showing nothing.
        if (listing.Type == ListingType.Produce)
        {
            ViewBag.Reference = await _db.MarketPrices
                .Where(p => p.CategoryId == listing.CategoryId && p.Unit == listing.PriceUnit)
                .OrderBy(p => p.District == listing.District ? 0 : 1)
                .ThenByDescending(p => p.AsOfDate)
                .Include(p => p.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        ViewBag.Slug = type;
        ViewBag.CanEdit = CanEdit(listing);
        return View(listing);
    }

    [HttpGet("create"), Authorize]
    public async Task<IActionResult> Create(string type)
    {
        var listingType = ListingSlug.ToType(type);
        await PopulateForm(listingType, excludeTasksAlreadyTaken: true);

        var user = await _users.GetUserAsync(User);
        return View(new Listing
        {
            Type = listingType,
            // Pre-fill from the profile: most people list from where they live.
            Village = user?.Village,
            Town = user?.Town,
            District = user?.District ?? "",
            PriceUnit = listingType == ListingType.Produce ? PriceUnit.PerKg : PriceUnit.PerDay
        });
    }

    [HttpPost("create"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string type,
        [Bind("Title,Description,CategoryId,Price,PriceUnit,Quantity,Village,Town,District,Skills,ExperienceYears")]
        Listing listing,
        List<IFormFile>? images)
    {
        var listingType = ListingSlug.ToType(type);

        // Never bound from the form - the client does not get to choose these.
        listing.Type = listingType;
        listing.OwnerId = _users.GetUserId(User)!;
        listing.Status = ListingStatus.Active;
        listing.CreatedAt = DateTime.UtcNow;

        // OwnerId is [Required] but excluded from binding, so validation flags it as
        // missing before we fill it in. Drop that error now the real value is set -
        // otherwise the form fails with nothing shown against any visible field.
        ModelState.Remove(nameof(Listing.OwnerId));

        ValidateForType(listing, listingType);

        if (!ModelState.IsValid)
        {
            await PopulateForm(listingType, excludeTasksAlreadyTaken: true);
            return View(listing);
        }

        _db.Listings.Add(listing);
        await _db.SaveChangesAsync();

        await SaveImagesAsync(listing, images);
        await _db.SaveChangesAsync();

        // One whole sentence rather than a noun dropped into a template. A translator
        // needs the finished sentence, and in Tamil the verb does not stay put while
        // only the noun changes.
        TempData["Message"] = "Your listing has been posted.";
        return RedirectToAction(nameof(Details), new { type, id = listing.Id });
    }

    [HttpGet("{id:int}/edit"), Authorize]
    public async Task<IActionResult> Edit(string type, int id)
    {
        var listing = await _db.Listings.Include(l => l.Images)
            .FirstOrDefaultAsync(l => l.Id == id && l.Type == ListingSlug.ToType(type));

        if (listing is null) return NotFound();
        if (!CanEdit(listing)) return Forbid();

        await PopulateForm(listing.Type);
        return View(listing);
    }

    [HttpPost("{id:int}/edit"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string type, int id, List<IFormFile>? images)
    {
        var listing = await _db.Listings.Include(l => l.Images)
            .FirstOrDefaultAsync(l => l.Id == id && l.Type == ListingSlug.ToType(type));

        if (listing is null) return NotFound();
        if (!CanEdit(listing)) return Forbid();

        // TryUpdateModel with an explicit allow-list: Owner, Type and CreatedAt stay put
        // however the form is tampered with.
        var updated = await TryUpdateModelAsync(listing, "",
            l => l.Title, l => l.Description, l => l.CategoryId, l => l.Price, l => l.PriceUnit,
            l => l.Quantity, l => l.Village, l => l.Town, l => l.District,
            l => l.Skills, l => l.ExperienceYears, l => l.Status);

        ValidateForType(listing, listing.Type);

        if (!updated || !ModelState.IsValid)
        {
            await PopulateForm(listing.Type);
            return View(listing);
        }

        await SaveImagesAsync(listing, images);
        await _db.SaveChangesAsync();

        TempData["Message"] = "Listing updated.";
        return RedirectToAction(nameof(Details), new { type, id });
    }

    [HttpPost("{id:int}/delete"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string type, int id)
    {
        var listing = await _db.Listings.FirstOrDefaultAsync(l => l.Id == id);
        if (listing is null) return NotFound();
        if (!CanEdit(listing)) return Forbid();

        // Soft delete: booking history must survive (SOW §2.3.2).
        listing.Status = ListingStatus.Removed;
        await _db.SaveChangesAsync();

        TempData["Message"] = "Listing removed.";
        return RedirectToAction(nameof(Index), new { type });
    }

    [HttpPost("{id:int}/images/{imageId:int}/delete"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(string type, int id, int imageId)
    {
        var listing = await _db.Listings.Include(l => l.Images).FirstOrDefaultAsync(l => l.Id == id);
        if (listing is null) return NotFound();
        if (!CanEdit(listing)) return Forbid();

        var image = listing.Images.FirstOrDefault(i => i.Id == imageId);
        if (image is not null)
        {
            _db.ListingImages.Remove(image);
            await _db.SaveChangesAsync();
            var path = Path.Combine(UploadFolder(), image.FileName);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }

        return RedirectToAction(nameof(Edit), new { type, id });
    }

    /// <summary>
    /// Anyone signed in may report a listing - the half of SOW §2.3.1 the document
    /// leaves out. It lives here rather than on AdminController because that class
    /// is [Authorize(Roles = Administrator)], and the only way to let ordinary
    /// members in there would be [AllowAnonymous], which switches authorization off
    /// altogether - including the [Authorize] beside it.
    /// </summary>
    [HttpPost("{id:int}/report"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(string type, int id, string reason)
    {
        var listing = await _db.Listings.FirstOrDefaultAsync(l => l.Id == id);
        if (listing is null) return NotFound();

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Say what is wrong with the listing.";
        }
        else
        {
            _db.ListingReports.Add(new ListingReport
            {
                ListingId = id,
                ReportedById = _users.GetUserId(User)!,
                Reason = reason.Trim()
            });
            await _db.SaveChangesAsync();
            TempData["Message"] = "Thank you. An administrator will look at this listing.";
        }

        return RedirectToAction(nameof(Details), new { type, id });
    }

    // --- helpers ---

    private bool CanEdit(Listing listing) =>
        User.Identity?.IsAuthenticated == true &&
        (listing.OwnerId == _users.GetUserId(User) || User.IsInRole(DbSeeder.AdminRole));

    private string UploadFolder() => Path.Combine(_env.WebRootPath, "uploads");

    /// <param name="excludeTasksAlreadyTaken">
    /// On a new labour registration, hide the tasks this worker has already registered
    /// for. A worker gets one entry per task (enforced by a unique index), so offering
    /// a duplicate only leads to an error they cannot act on.
    /// </param>
    private async Task PopulateForm(ListingType type, bool excludeTasksAlreadyTaken = false)
    {
        var categories = _db.Categories.Where(c => c.ListingType == type);

        if (excludeTasksAlreadyTaken && type == ListingType.Labour)
        {
            var me = _users.GetUserId(User);
            var taken = _db.Listings
                .Where(l => l.Type == ListingType.Labour && l.OwnerId == me
                            && l.Status != ListingStatus.Removed)
                .Select(l => l.CategoryId);

            categories = categories.Where(c => !taken.Contains(c.Id));
        }

        var list = await categories.OrderBy(c => c.Group).ThenBy(c => c.Name)
            .AsNoTracking().ToListAsync();

        // Sorted by the name the reader actually sees. Ordering by the English in SQL
        // and then showing Tamil leaves a dropdown that looks shuffled.
        list = [.. list.OrderBy(c => c.Group is null ? "" : _text[c.Group].Value)
                       .ThenBy(c => _text[c.Name].Value, StringComparer.CurrentCulture)];

        ViewBag.Categories = Choices.CategoryList(_text, list);

        // The form needs the suggested unit per option, which a SelectList cannot
        // carry, so the raw list goes along too.
        ViewBag.CategoryList = list;

        ViewBag.Districts = Choices.DistrictList(_text);
    }

    /// <summary>Fields the SOW requires per module but the shared table cannot make mandatory.</summary>
    private void ValidateForType(Listing listing, ListingType type)
    {
        if (type == ListingType.Produce && listing.Quantity is null or <= 0)
            ModelState.AddModelError(nameof(Listing.Quantity), "Enter the quantity available.");

        if (type == ListingType.Labour && string.IsNullOrWhiteSpace(listing.Skills))
            ModelState.AddModelError(nameof(Listing.Skills), "Describe the work you can do.");

        if (!string.IsNullOrEmpty(listing.District) && !Districts.All.Contains(listing.District))
            ModelState.AddModelError(nameof(Listing.District), "Choose a district from the list.");
    }

    private async Task SaveImagesAsync(Listing listing, List<IFormFile>? images)
    {
        if (images is null || images.Count == 0) return;

        var folder = UploadFolder();
        Directory.CreateDirectory(folder);

        var room = MaxImagesPerListing - listing.Images.Count;

        foreach (var file in images.Where(f => f.Length > 0).Take(Math.Max(room, 0)))
        {
            // Trust the extension for routing only, never the caller's filename for the path.
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext) || file.Length > MaxImageBytes) continue;

            var name = $"{Guid.NewGuid():N}{ext}";
            await using var stream = System.IO.File.Create(Path.Combine(folder, name));
            await file.CopyToAsync(stream);

            listing.Images.Add(new ListingImage { FileName = name });
        }
    }
}
