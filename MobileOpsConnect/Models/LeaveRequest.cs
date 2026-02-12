using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobileOpsConnect.Models
{
    public class LeaveRequest
    {
        [Key]
        public int LeaveID { get; set; }

        // --- LINK TO USER (Employee) ---
        // We use the UserID string (Guid) to link to the logged-in user
        [Required]
        public string UserID { get; set; }

        [ForeignKey("UserID")]
        public virtual IdentityUser? User { get; set; }

        // --- LEAVE DETAILS ---
        [Required]
        [Display(Name = "Leave Type")]
        public string LeaveType { get; set; } // e.g., Sick, Vacation

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Required]
        public string Reason { get; set; }

        // --- APPROVAL WORKFLOW ---
        // Default status is "Pending"
        public string Status { get; set; } = "Pending";

        // Who approved it? (Nullable, because it starts as null)
        public string? ApprovedById { get; set; }

        public DateTime DateRequested { get; set; } = DateTime.Now;
    }
}