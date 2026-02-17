using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace MobileOpsConnect.Models
{
    public class AccountingEntry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Transaction Date")]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20)]
        public string Type { get; set; } = "Expense"; // Income, Expense

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = string.Empty; // e.g., Purchase Order, Payroll, Sales, Utilities

        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal Amount { get; set; }

        [Display(Name = "Reference #")]
        [StringLength(50)]
        public string? ReferenceNumber { get; set; }

        // Optional link to a Purchase Order
        public int? PurchaseOrderId { get; set; }

        [ForeignKey("PurchaseOrderId")]
        public PurchaseOrder? PurchaseOrder { get; set; }

        // Who recorded this entry?
        [Required]
        public string RecordedById { get; set; } = string.Empty;

        [ForeignKey("RecordedById")]
        public IdentityUser? RecordedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
