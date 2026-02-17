using System.ComponentModel.DataAnnotations;

namespace MobileOpsConnect.Models
{
    public class SystemSetting
    {
        [Key]
        public int Id { get; set; } // We will always use Row #1

        [Required]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = "MobileOps Hardware";

        [Required]
        [Display(Name = "Support Contact Email")]
        public string SupportEmail { get; set; } = "support@mobileops.com";

        [Display(Name = "Low Stock Warning Threshold")]
        public int LowStockThreshold { get; set; } = 10;

        [Display(Name = "Tax Rate (%)")]
        public decimal TaxRate { get; set; } = 12.00m; // Default VAT

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}