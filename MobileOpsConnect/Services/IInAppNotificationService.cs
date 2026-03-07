namespace MobileOpsConnect.Services
{
    public interface IInAppNotificationService
    {
        Task CreateAsync(string userId, string title, string message, string type, string? icon = null, string? url = null);
        Task CreateForRoleAsync(string roleName, string title, string message, string type, string? icon = null, string? url = null);
        Task CreateForAllAsync(string title, string message, string type, string? icon = null, string? url = null);
    }
}
