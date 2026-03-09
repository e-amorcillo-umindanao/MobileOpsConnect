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
        public async Task<IActionResult> Index(string? status, int? page)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var query = _context.InAppNotifications
                .Where(n => n.UserId == userId);

            // Calculate counts for summary
            ViewBag.TotalCount = await query.CountAsync();
            ViewBag.UnreadCount = await query.CountAsync(n => !n.IsRead);
            ViewBag.ReadCount = await query.CountAsync(n => n.IsRead);

            // Get most common notification type
            ViewBag.TopType = await query.GroupBy(n => n.Type)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefaultAsync() ?? "None";

            string currentStatus = status ?? "unread";
            ViewBag.CurrentStatus = currentStatus;

            if (currentStatus == "unread")
            {
                query = query.Where(n => !n.IsRead);
            }

            var notifications = query.OrderByDescending(n => n.CreatedAt);

            return View(await PaginatedList<InAppNotification>.CreateAsync(notifications.AsNoTracking(), page ?? 1, 10));
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
        [IgnoreAntiforgeryToken]
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
        [IgnoreAntiforgeryToken]
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

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ClearAll()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var notifications = await _context.InAppNotifications
                .Where(n => n.UserId == userId)
                .ToListAsync();

            if (notifications.Any())
            {
                _context.InAppNotifications.RemoveRange(notifications);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, deletedCount = notifications.Count });
        }
    }
}
