using AgriConnect.Data;
using Microsoft.AspNetCore.Identity;
using AgriConnect.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriConnect.ViewComponents;

/// <summary>
/// One COUNT per page render. No SignalR, no polling - SOW §2.3.6 asks for in-app
/// notification of booking activity, which a badge on each page load satisfies.
/// </summary>
public class NotificationBellViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public NotificationBellViewComponent(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = _users.GetUserId(HttpContext.User);
        if (userId is null) return Content(string.Empty);

        var unread = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return View(unread);
    }
}
