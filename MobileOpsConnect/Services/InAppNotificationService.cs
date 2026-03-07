using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Hubs;
using MobileOpsConnect.Models;

namespace MobileOpsConnect.Services
{
    public class InAppNotificationService : IInAppNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<InventoryHub> _hubContext;
        private readonly UserManager<IdentityUser> _userManager;

        public InAppNotificationService(
            ApplicationDbContext context,
            IHubContext<InventoryHub> hubContext,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _hubContext = hubContext;
            _userManager = userManager;
        }

        public async Task CreateAsync(string userId, string title, string message, string type, string? icon = null, string? url = null)
        {
            var notification = new InAppNotification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                Icon = icon,
                Url = url,
                CreatedAt = PhilippineTime.Now
            };

            _context.InAppNotifications.Add(notification);
            await _context.SaveChangesAsync();

            // Notify via SignalR (to the specific user)
            await _hubContext.Clients.User(userId).SendAsync("NewNotification", new
            {
                id = notification.Id,
                title = notification.Title,
                message = notification.Message,
                icon = notification.Icon,
                url = notification.Url,
                type = notification.Type,
                createdAt = notification.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss")
            });
        }

        public async Task CreateForRoleAsync(string roleName, string title, string message, string type, string? icon = null, string? url = null)
        {
            // Get all user IDs in this role
            var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
            var userIds = usersInRole.Select(u => u.Id).ToList();

            if (!userIds.Any()) return;

            var notifications = userIds.Select(uid => new InAppNotification
            {
                UserId = uid,
                Title = title,
                Message = message,
                Type = type,
                Icon = icon,
                Url = url,
                CreatedAt = PhilippineTime.Now
            }).ToList();

            _context.InAppNotifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            // Notify via SignalR (to the role group)
            await _hubContext.Clients.Group($"role_{roleName}").SendAsync("NewNotification", new
            {
                title,
                message,
                icon,
                url,
                type,
                createdAt = PhilippineTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            });
        }

        public async Task CreateForAllAsync(string title, string message, string type, string? icon = null, string? url = null)
        {
            var userIds = await _context.Users.Select(u => u.Id).ToListAsync();

            if (!userIds.Any()) return;

            var notifications = userIds.Select(uid => new InAppNotification
            {
                UserId = uid,
                Title = title,
                Message = message,
                Type = type,
                Icon = icon,
                Url = url,
                CreatedAt = PhilippineTime.Now
            }).ToList();

            _context.InAppNotifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            // Notify via SignalR (to everyone)
            await _hubContext.Clients.All.SendAsync("NewNotification", new
            {
                title,
                message,
                icon,
                url,
                type,
                createdAt = PhilippineTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            });
        }
    }
}
