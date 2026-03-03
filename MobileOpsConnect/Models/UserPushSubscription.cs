using System.ComponentModel.DataAnnotations;
using MobileOpsConnect.Services;

namespace MobileOpsConnect.Models
{
    /// <summary>
    /// Stores the raw Web Push subscription (endpoint + keys) for standard Web Push delivery.
    /// This is used alongside FCM tokens for iOS Safari compatibility.
    /// </summary>
    public class UserPushSubscription
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Endpoint { get; set; } = string.Empty;

        [Required]
        public string P256dh { get; set; } = string.Empty;

        [Required]
        public string Auth { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = PhilippineTime.Now;
    }
}
