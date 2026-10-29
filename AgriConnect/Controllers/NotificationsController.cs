using AgriConnect.Data;
using AgriConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriConnect.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public NotificationsController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index()
    {
        var me = _users.GetUserId(User)!;
        var notifications = await _db.Notifications
            .Where(n => n.UserId == me)
            .OrderByDescending(n => n.CreatedAt)
            .AsNoTracking()
            .Take(100)
            .ToListAsync();

        return View(notifications);
    }

    /// <summary>Mark read and follow the link in one step - that is what clicking one means.</summary>
    [HttpGet]
    public async Task<IActionResult> Open(int id)
    {
        var me = _users.GetUserId(User)!;
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == me);
        if (notification is null) return NotFound();

        notification.IsRead = true;
        await _db.SaveChangesAsync();

        return LocalRedirect(notification.LinkUrl ?? Url.Action(nameof(Index))!);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var me = _users.GetUserId(User)!;
        await _db.Notifications
            .Where(n => n.UserId == me && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        return RedirectToAction(nameof(Index));
    }
}
