using AgriConnect.Data;
using AgriConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace AgriConnect.Controllers;

/// <summary>
/// Administrator-maintained reference prices (SOW §2.3.7). Stored figures only -
/// nothing is predicted, and the comparison is a subtraction done in the view.
/// </summary>
public class MarketPricesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<SharedResource> _text;

    public MarketPricesController(ApplicationDbContext db, IStringLocalizer<SharedResource> text)
    {
        _db = db;
        _text = text;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(string? district, int? categoryId)
    {
        var q = _db.MarketPrices.Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(district)) q = q.Where(p => p.District == district);
        if (categoryId is int id) q = q.Where(p => p.CategoryId == id);

        ViewBag.Districts = Choices.DistrictList(_text, district);
        ViewBag.Categories = Choices.CategoryList(_text,
            await _db.Categories.Where(c => c.ListingType == ListingType.Produce)
                .AsNoTracking().ToListAsync(),
            categoryId);
        ViewBag.District = district;
        ViewBag.CategoryId = categoryId;

        return View(await q.OrderBy(p => p.Category!.Name).ThenBy(p => p.District)
            .AsNoTracking().ToListAsync());
    }

    [Authorize(Roles = DbSeeder.AdminRole)]
    public async Task<IActionResult> Edit(int? id)
    {
        await PopulateForm();
        if (id is null)
            return View(new MarketPrice { AsOfDate = DateOnly.FromDateTime(DateTime.Today), Unit = PriceUnit.PerKg });

        var price = await _db.MarketPrices.FindAsync(id);
        return price is null ? NotFound() : View(price);
    }

    [HttpPost, Authorize(Roles = DbSeeder.AdminRole), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int? id, [Bind("CategoryId,District,Price,Unit,Source,AsOfDate")] MarketPrice input)
    {
        if (!Districts.All.Contains(input.District))
            ModelState.AddModelError(nameof(MarketPrice.District), "Choose a district from the list.");

        if (!ModelState.IsValid)
        {
            await PopulateForm();
            return View(input);
        }

        if (id is null)
        {
            _db.MarketPrices.Add(input);
        }
        else
        {
            var existing = await _db.MarketPrices.FindAsync(id);
            if (existing is null) return NotFound();

            existing.CategoryId = input.CategoryId;
            existing.District = input.District;
            existing.Price = input.Price;
            existing.Unit = input.Unit;
            existing.Source = input.Source;
            existing.AsOfDate = input.AsOfDate;
        }

        await _db.SaveChangesAsync();
        TempData["Message"] = "Reference price saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = DbSeeder.AdminRole), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _db.MarketPrices.Where(p => p.Id == id).ExecuteDeleteAsync();
        TempData["Message"] = "Reference price removed.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateForm()
    {
        ViewBag.CategoryList = Choices.CategoryList(_text,
            await _db.Categories.Where(c => c.ListingType == ListingType.Produce)
                .AsNoTracking().ToListAsync());
        ViewBag.DistrictList = Choices.DistrictList(_text);
    }
}
