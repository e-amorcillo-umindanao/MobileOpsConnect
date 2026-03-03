using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;
using System.Text.Json;
using WebPush;

namespace MobileOpsConnect.Services
{
    /// <summary>
    /// Sends push notifications using BOTH Firebase Cloud Messaging and the standard Web Push protocol.
    /// Standard Web Push is required for iOS Safari PWA, which FCM doesn't reliably support.
    /// </summary>
    public class FcmNotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FcmNotificationService> _logger;
        private readonly WebPushClient _webPushClient;
        private readonly VapidDetails _vapidDetails;

        public FcmNotificationService(
            ApplicationDbContext context,
            ILogger<FcmNotificationService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _webPushClient = new WebPushClient();

            // Load VAPID details from config (for standard Web Push)
            var vapidSubject = configuration["Vapid:Subject"] ?? "mailto:admin@mobileops.com";
            var vapidPublicKey = configuration["Vapid:PublicKey"] ?? "";
            var vapidPrivateKey = configuration["Vapid:PrivateKey"] ?? "";

            if (!string.IsNullOrEmpty(vapidPublicKey) && !string.IsNullOrEmpty(vapidPrivateKey))
            {
                _vapidDetails = new VapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);
            }
            else
            {
                _logger.LogWarning("VAPID keys not configured. Standard Web Push will not work.");
                _vapidDetails = null!;
            }
        }

        public async Task<int> SendToUserAsync(string userId, string title, string body, string? url = null)
        {
            int fcmCount = await SendFcmToUserAsync(userId, title, body, url);
            int webPushCount = await SendWebPushToUserAsync(userId, title, body, url);

            _logger.LogInformation("Notification sent to user {UserId}: FCM={FcmCount}, WebPush={WebPushCount}",
                userId, fcmCount, webPushCount);

            return fcmCount + webPushCount;
        }

        public async Task<int> SendToAllAsync(string title, string body, string? url = null)
        {
            int fcmCount = await SendFcmToAllAsync(title, body, url);
            int webPushCount = await SendWebPushToAllAsync(title, body, url);

            return fcmCount + webPushCount;
        }

        public async Task<int> SendToRoleAsync(string roleName, string title, string body, string? url = null)
        {
            // Get all user IDs that belong to the target role
            var userIdsInRole = await _context.UserRoles
                .Join(_context.Roles,
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => new { ur.UserId, RoleName = r.Name })
                .Where(x => x.RoleName == roleName)
                .Select(x => x.UserId)
                .ToListAsync();

            if (userIdsInRole.Count == 0)
            {
                _logger.LogWarning("No users found in role {Role} for notification", roleName);
                return 0;
            }

            int fcmCount = 0;
            int webPushCount = 0;

            // FCM
            var fcmTokens = await _context.UserFcmTokens
                .Where(t => userIdsInRole.Contains(t.UserId))
                .Select(t => t.Token)
                .Distinct()
                .ToListAsync();

            if (fcmTokens.Count > 0)
                fcmCount = await SendFcmToTokensAsync(fcmTokens, title, body, url);

            // Web Push
            var subscriptions = await _context.UserPushSubscriptions
                .Where(s => userIdsInRole.Contains(s.UserId))
                .ToListAsync();

            if (subscriptions.Count > 0)
                webPushCount = await SendWebPushToSubscriptionsAsync(subscriptions, title, body, url);

            return fcmCount + webPushCount;
        }

        // ────── FCM Methods ──────

        private async Task<int> SendFcmToUserAsync(string userId, string title, string body, string? url)
        {
            var tokens = await _context.UserFcmTokens
                .Where(t => t.UserId == userId)
                .Select(t => t.Token)
                .ToListAsync();

            if (tokens.Count == 0) return 0;
            return await SendFcmToTokensAsync(tokens, title, body, url);
        }

        private async Task<int> SendFcmToAllAsync(string title, string body, string? url)
        {
            var tokens = await _context.UserFcmTokens
                .Select(t => t.Token).Distinct().ToListAsync();

            if (tokens.Count == 0) return 0;
            return await SendFcmToTokensAsync(tokens, title, body, url);
        }

        private async Task<int> SendFcmToTokensAsync(List<string> tokens, string title, string body, string? url)
        {
            var notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body };
            var data = new Dictionary<string, string> { { "title", title }, { "body", body } };
            if (!string.IsNullOrEmpty(url)) data["url"] = url;

            int successCount = 0;
            var staleTokens = new List<string>();

            foreach (var batch in tokens.Chunk(500))
            {
                var message = new FirebaseAdmin.Messaging.MulticastMessage
                {
                    Tokens = batch.ToList(),
                    Notification = notification,
                    Data = data,
                    Webpush = new FirebaseAdmin.Messaging.WebpushConfig
                    {
                        FcmOptions = new FirebaseAdmin.Messaging.WebpushFcmOptions { Link = url ?? "/" }
                    }
                };

                try
                {
                    var response = await FirebaseAdmin.Messaging.FirebaseMessaging.DefaultInstance
                        .SendEachForMulticastAsync(message);
                    successCount += response.SuccessCount;

                    for (int i = 0; i < response.Responses.Count; i++)
                    {
                        if (!response.Responses[i].IsSuccess)
                        {
                            staleTokens.Add(batch[i]);
                            _logger.LogWarning("FCM send failed for token: {Error}",
                                response.Responses[i].Exception?.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending FCM multicast message");
                }
            }

            // Clean up stale tokens
            if (staleTokens.Count > 0)
            {
                var tokensToRemove = await _context.UserFcmTokens
                    .Where(t => staleTokens.Contains(t.Token)).ToListAsync();
                _context.UserFcmTokens.RemoveRange(tokensToRemove);
                await _context.SaveChangesAsync();
            }

            return successCount;
        }

        // ────── Standard Web Push Methods ──────

        private async Task<int> SendWebPushToUserAsync(string userId, string title, string body, string? url)
        {
            var subscriptions = await _context.UserPushSubscriptions
                .Where(s => s.UserId == userId)
                .ToListAsync();

            if (subscriptions.Count == 0) return 0;
            return await SendWebPushToSubscriptionsAsync(subscriptions, title, body, url);
        }

        private async Task<int> SendWebPushToAllAsync(string title, string body, string? url)
        {
            var subscriptions = await _context.UserPushSubscriptions.ToListAsync();
            if (subscriptions.Count == 0) return 0;
            return await SendWebPushToSubscriptionsAsync(subscriptions, title, body, url);
        }

        private async Task<int> SendWebPushToSubscriptionsAsync(
            List<UserPushSubscription> subscriptions, string title, string body, string? url)
        {
            if (_vapidDetails == null)
            {
                _logger.LogWarning("Cannot send Web Push: VAPID keys not configured");
                return 0;
            }

            var payload = JsonSerializer.Serialize(new
            {
                notification = new { title, body },
                data = new { url = url ?? "/" }
            });

            int successCount = 0;
            var staleIds = new System.Collections.Concurrent.ConcurrentBag<int>();

            // Send all Web Push notifications in parallel (instead of one-by-one)
            var tasks = subscriptions.Select(async sub =>
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                    await _webPushClient.SendNotificationAsync(pushSub, payload, _vapidDetails, cts.Token);
                    Interlocked.Increment(ref successCount);
                    _logger.LogDebug("Web Push sent to endpoint: {Endpoint}", sub.Endpoint[..50]);
                }
                catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                                                   ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    staleIds.Add(sub.Id);
                    _logger.LogWarning("Web Push subscription expired: {Endpoint}", sub.Endpoint[..50]);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Web Push timed out for endpoint: {Endpoint}", sub.Endpoint[..Math.Min(50, sub.Endpoint.Length)]);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Web Push failed for endpoint: {Endpoint}", sub.Endpoint[..Math.Min(50, sub.Endpoint.Length)]);
                }
            });
            await Task.WhenAll(tasks);

            // Clean up stale subscriptions
            if (!staleIds.IsEmpty)
            {
                var idsToRemove = staleIds.ToList();
                var toRemove = await _context.UserPushSubscriptions
                    .Where(s => idsToRemove.Contains(s.Id)).ToListAsync();
                _context.UserPushSubscriptions.RemoveRange(toRemove);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Web Push sent: {Success}/{Total}", successCount, subscriptions.Count);
            return successCount;
        }
    }
}
