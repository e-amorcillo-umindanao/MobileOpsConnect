using System.ComponentModel.DataAnnotations;

namespace MobileOpsConnect.Models
{
    public class InAppNotification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty; // Recipient

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        public string? Icon { get; set; } // Bootstrap icon class, e.g., "bi-check-circle"
        
        public string? Url { get; set; } // Click-through link

        [Required]
        public string Type { get; set; } = "General"; // Leave, Stock, Order, User, Accounting

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = MobileOpsConnect.Services.PhilippineTime.Now;
    }
}
