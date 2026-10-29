using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace AgriConnect.Controllers;

public class CultureController : Controller
{
    /// <summary>
    /// Remembers the chosen language in a cookie, which the cookie culture provider
    /// reads on every later request. A cookie rather than a /ta/ URL prefix: every
    /// link, form action and redirect in the application would otherwise have to
    /// carry the language, and a member who shares a link would hand the recipient
    /// their own language rather than the recipient's.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Set(string culture, string? returnUrl)
    {
        // Only the two we actually ship. Anything else is ignored rather than stored,
        // so a hand-edited form cannot park an arbitrary culture in a member's cookie.
        if (culture is "en" or "ta")
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,       // a language choice is not tracking
                    HttpOnly = false,         // readable by script if the UI ever needs it
                    SameSite = SameSiteMode.Lax
                });
        }

        // LocalRedirect would throw on a tampered value; this sends them home instead.
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Home");
    }
}
