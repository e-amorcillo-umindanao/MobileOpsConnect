namespace MobileOpsConnect.Services
{
    public interface IAuditService
    {
        Task LogAsync(string userId, string email, string role, string action, string details, string? ipAddress = null, bool isCritical = false);
    }
}
