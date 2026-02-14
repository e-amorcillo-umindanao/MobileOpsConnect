using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;

namespace MobileOpsConnect.Services
{
    public class FcmNotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FcmNotificationService> _logger;

        public FcmNotificationService(ApplicationDbContext context, ILogger<FcmNotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<int> SendToUserAsync(string userId, string title, string body, string? url = null)
        {
            var tokens = await _context.UserFcmTokens
                .Where(t => t.UserId == userId)
                .Select(t => t.Token)
                .ToListAsync();

            if (tokens.Count == 0)
            {
                _logger.LogWarning("No FCM tokens found for user {UserId}", userId);
                return 0;
            }

            return await SendToTokensAsync(tokens, title, body, url);
        }

        public async Task<int> SendToAllAsync(string title, string body, string? url = null)
        {
            var tokens = await _context.UserFcmTokens
                .Select(t => t.Token)
                .Distinct()
                .ToListAsync();

            if (tokens.Count == 0)
            {
                _logger.LogWarning("No FCM tokens registered in the system.");
                return 0;
            }

            return await SendToTokensAsync(tokens, title, body, url);
        }

        private async Task<int> SendToTokensAsync(List<string> tokens, string title, string body, string? url)
        {
            var notification = new Notification
            {
                Title = title,
                Body = body,
            };

            var data = new Dictionary<string, string>
            {
                { "title", title },
                { "body", body },
            };

            if (!string.IsNullOrEmpty(url))
            {
                data["url"] = url;
            }

            int successCount = 0;
            var staleTokens = new List<string>();

            // Firebase allows up to 500 tokens per MulticastMessage
            foreach (var batch in tokens.Chunk(500))
            {
                var message = new MulticastMessage
                {
                    Tokens = batch.ToList(),
                    Notification = notification,
                    Data = data,
                    Webpush = new WebpushConfig
                    {
                        FcmOptions = new WebpushFcmOptions
                        {
                            Link = url ?? "/"
                        }
                    }
                };

                try
                {
                    var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
                    successCount += response.SuccessCount;

                    // Track stale/invalid tokens for cleanup
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

            // Clean up stale/invalid tokens
            if (staleTokens.Count > 0)
            {
                var tokensToRemove = await _context.UserFcmTokens
                    .Where(t => staleTokens.Contains(t.Token))
                    .ToListAsync();

                _context.UserFcmTokens.RemoveRange(tokensToRemove);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Removed {Count} stale FCM tokens", tokensToRemove.Count);
            }

            _logger.LogInformation("FCM notification sent: {Success}/{Total} successful",
                successCount, tokens.Count);

            return successCount;
        }
    }
}
