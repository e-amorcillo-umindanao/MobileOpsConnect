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
        private readonly IConfiguration _configuration;

        public NotificationController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            INotificationService notificationService,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _configuration = configuration;
        }

        /// <summary>
        /// Returns the VAPID public key for standard Web Push subscriptions (iOS Safari).
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetVapidKey()
        {
            var key = _configuration["Vapid:PublicKey"] ?? "";
            return Ok(new { publicKey = key });
        }

        /// <summary>
        /// Saves or updates the FCM token for the currently logged-in user.
        /// Called automatically from the client-side JavaScript.
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
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
                existingToken.CreatedAt = PhilippineTime.Now;
            }
            else
            {
                // Register new token
                _context.UserFcmTokens.Add(new UserFcmToken
                {
                    UserId = userId,
                    Token = request.Token,
                    CreatedAt = PhilippineTime.Now,
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

        /// <summary>
        /// Diagnostic: sends a test directly via Firebase and returns per-token errors.
        /// Hit /Notification/TestSelf in the browser.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> TestSelf()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized("Not logged in");

            var tokens = await _context.UserFcmTokens
                .Where(t => t.UserId == userId)
                .Select(t => t.Token)
                .ToListAsync();

            if (tokens.Count == 0)
                return Ok(new { success = false, message = "No FCM tokens found. Hard-reload (Ctrl+Shift+R) and try again.", tokenCount = 0 });

            try
            {
                var message = new FirebaseAdmin.Messaging.MulticastMessage
                {
                    Tokens = tokens,
                    Notification = new FirebaseAdmin.Messaging.Notification
                    {
                        Title = "🔔 Test Notification",
                        Body = "If you see this toast, FCM is working!"
                    },
                    Data = new Dictionary<string, string> { { "title", "Test" }, { "body", "Test" } }
                };

                var response = await FirebaseAdmin.Messaging.FirebaseMessaging.DefaultInstance
                    .SendEachForMulticastAsync(message);

                var errors = new List<string>();
                for (int i = 0; i < response.Responses.Count; i++)
                {
                    if (!response.Responses[i].IsSuccess)
                    {
                        errors.Add($"Token #{i+1}: {response.Responses[i].Exception?.MessagingErrorCode} — {response.Responses[i].Exception?.Message}");
                    }
                }

                return Ok(new
                {
                    success = response.SuccessCount > 0,
                    sent = response.SuccessCount,
                    failed = response.FailureCount,
                    tokenCount = tokens.Count,
                    errors = errors
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Firebase SDK error: {ex.GetType().Name}: {ex.Message}", tokenCount = tokens.Count });
            }
        }
        /// <summary>
        /// Stores the raw Web Push subscription for standard push delivery (iOS Safari).
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RegisterSubscription([FromBody] RegisterSubscriptionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Endpoint))
                return BadRequest(new { success = false, message = "Endpoint is required" });

            var userId = _userManager.GetUserId(User);
            if (userId == null)
                return Unauthorized(new { success = false, message = "Not authenticated" });

            // Check if this subscription already exists
            var existing = await _context.UserPushSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == request.Endpoint);

            if (existing != null)
            {
                existing.P256dh = request.P256dh;
                existing.Auth = request.Auth;
                existing.CreatedAt = PhilippineTime.Now;
            }
            else
            {
                _context.UserPushSubscriptions.Add(new UserPushSubscription
                {
                    UserId = userId,
                    Endpoint = request.Endpoint,
                    P256dh = request.P256dh,
                    Auth = request.Auth,
                    CreatedAt = PhilippineTime.Now,
                });
            }

            await _context.SaveChangesAsync();

            // Clean up stale FCM tokens for this user (we no longer use FCM)
            var staleFcmTokens = await _context.UserFcmTokens
                .Where(t => t.UserId == userId).ToListAsync();
            if (staleFcmTokens.Count > 0)
            {
                _context.UserFcmTokens.RemoveRange(staleFcmTokens);
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, message = "Push subscription registered" });
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

    public class RegisterSubscriptionRequest
    {
        public string Endpoint { get; set; } = string.Empty;
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
    }
}
