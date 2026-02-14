namespace MobileOpsConnect.Services
{
    public interface INotificationService
    {
        /// <summary>
        /// Send a push notification to a specific user by their Identity UserId.
        /// </summary>
        Task<int> SendToUserAsync(string userId, string title, string body, string? url = null);

        /// <summary>
        /// Send a push notification to all registered users.
        /// </summary>
        Task<int> SendToAllAsync(string title, string body, string? url = null);
    }
}
