using AgriConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace AgriConnect.Controllers;

/// <summary>
/// The member's own details. Identity's default UI covers the password and the email;
/// this covers what AgriConnect itself needs - where you are, which the labour search
/// ranks on, and how to reach you, which buyers need.
/// </summary>
[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IStringLocalizer<SharedResource> _text;

    public ProfileController(UserManager<ApplicationUser> users, IStringLocalizer<SharedResource> text)
    {
        _users = users;
        _text = text;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();

        PopulateDistricts(user.District);

        return View(new ProfileForm
        {
            FullName = user.FullName ?? "",
            PhoneNumber = user.PhoneNumber,
            Gender = user.Gender,
            Village = user.Village,
            Town = user.Town,
            District = user.District ?? ""
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileForm form)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();

        if (!Districts.All.Contains(form.District))
            ModelState.AddModelError(nameof(ProfileForm.District), "Choose a district from the list.");

        if (!ModelState.IsValid)
        {
            PopulateDistricts(form.District);
            return View(form);
        }

        user.FullName = form.FullName.Trim();
        user.PhoneNumber = form.PhoneNumber?.Trim();
        user.Gender = form.Gender;
        user.Village = form.Village?.Trim();
        user.Town = form.Town?.Trim();
        user.District = form.District;

        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            PopulateDistricts(form.District);
            return View(form);
        }

        TempData["Message"] = "Your profile has been saved.";
        return RedirectToAction(nameof(Index));
    }

    private void PopulateDistricts(string? selected) =>
        ViewBag.Districts = Choices.DistrictList(_text, selected);
}
