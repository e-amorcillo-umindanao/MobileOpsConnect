using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;
using MobileOpsConnect.Services;

namespace MobileOpsConnect.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly INotificationService _notificationService;

        public NotificationController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Saves or updates the FCM token for the currently logged-in user.
        /// Called automatically from the client-side JavaScript.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterToken([FromBody] RegisterTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Token))
            {
                return BadRequest(new { success = false, message = "Token is required" });
            }

            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return Unauthorized(new { success = false, message = "Not authenticated" });
            }

            // Check if this exact token already exists for this user
            var existingToken = await _context.UserFcmTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == request.Token);

            if (existingToken != null)
            {
                // Token already registered — just update the timestamp
                existingToken.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                // Register new token
                _context.UserFcmTokens.Add(new UserFcmToken
                {
                    UserId = userId,
                    Token = request.Token,
                    CreatedAt = DateTime.UtcNow,
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "FCM token registered" });
        }

        /// <summary>
        /// Admin-only endpoint to send a test notification to all users.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin,SystemAdmin")]
        public async Task<IActionResult> SendTestNotification([FromBody] SendNotificationRequest request)
        {
            var title = request?.Title ?? "MobileOpsConnect";
            var body = request?.Body ?? "This is a test notification.";

            var sent = await _notificationService.SendToAllAsync(title, body);

            return Ok(new { success = true, message = $"Notification sent to {sent} device(s)." });
        }
    }

    // -- Request DTOs --

    public class RegisterTokenRequest
    {
        public string Token { get; set; } = string.Empty;
    }

    public class SendNotificationRequest
    {
        public string? Title { get; set; }
        public string? Body { get; set; }
    }
}
