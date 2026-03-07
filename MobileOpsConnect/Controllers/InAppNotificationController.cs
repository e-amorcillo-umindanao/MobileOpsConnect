using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;

namespace MobileOpsConnect.Controllers
{
    [Authorize]
    public class InAppNotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public InAppNotificationController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetRecent()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var notifications = await _context.InAppNotifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Icon,
                    n.Url,
                    n.Type,
                    n.IsRead,
                    createdAt = n.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss")
                })
                .ToListAsync();

            var unreadCount = await _context.InAppNotifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            return Json(new { notifications, unreadCount });
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var notification = await _context.InAppNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification == null) return NotFound();

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var unread = await _context.InAppNotifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unread.Any())
            {
                foreach (var n in unread)
                {
                    n.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }
    }
}
