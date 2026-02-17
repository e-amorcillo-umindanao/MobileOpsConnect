using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace MobileOpsConnect.Models
{
    public class PurchaseOrder
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedCost { get; set; }

        [Required]
        public string RequestedById { get; set; } = string.Empty;

        [ForeignKey("RequestedById")]
        public IdentityUser? RequestedBy { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        public string? ApprovedById { get; set; }

        [ForeignKey("ApprovedById")]
        public IdentityUser? ApprovedBy { get; set; }

        public DateTime DateRequested { get; set; } = DateTime.Now;

        public DateTime? DateProcessed { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
