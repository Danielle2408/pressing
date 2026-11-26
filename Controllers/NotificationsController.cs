using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pressing.Data;
using Pressing.ViewModels;

namespace Pressing.Controllers;

[Authorize]
[Route("Notifications/[action]")]
public sealed class NotificationsController(ApplicationDbContext database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();
        var userNotifications = database.UserNotifications.AsNoTracking().Where(item => item.UserId == userId);
        return View(new NotificationListViewModel
        {
            Notifications = await userNotifications.OrderByDescending(item => item.CreatedAtUtc).Take(100).ToListAsync(),
            UnreadCount = await userNotifications.CountAsync(item => !item.IsRead)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToutLire()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var unread = await database.UserNotifications.Where(item => item.UserId == userId && !item.IsRead).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var notification in unread) { notification.IsRead = true; notification.ReadAtUtc = now; }
        await database.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ouvrir(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var notification = await database.UserNotifications.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
        if (notification is null) return NotFound();
        notification.IsRead = true;
        notification.ReadAtUtc = DateTime.UtcNow;
        await database.SaveChangesAsync();
        return !string.IsNullOrWhiteSpace(notification.Url) && Url.IsLocalUrl(notification.Url)
            ? LocalRedirect(notification.Url)
            : RedirectToAction(nameof(Index));
    }
}
