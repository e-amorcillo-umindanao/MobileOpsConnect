using System.ComponentModel.DataAnnotations;
using MobileOpsConnect.Services;

namespace MobileOpsConnect.Models
{
    public class UserFcmToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = PhilippineTime.Now;
    }
}
