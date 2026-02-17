using MobileOpsConnect.Data;
using MobileOpsConnect.Models;

namespace MobileOpsConnect.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;

        public AuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string userId, string email, string role, string action, string details, string? ipAddress = null, bool isCritical = false)
        {
            var log = new AuditLog
            {
                Timestamp = DateTime.Now,
                UserId = userId,
                UserEmail = email,
                UserRole = role,
                Action = action,
                Details = details,
                IpAddress = ipAddress ?? "N/A",
                IsCritical = isCritical
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
