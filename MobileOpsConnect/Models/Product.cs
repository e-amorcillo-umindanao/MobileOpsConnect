using System.ComponentModel.DataAnnotations;

namespace MobileOpsConnect.Models
{
    public class Product
    {
        [Key]
        public int ProductID { get; set; }

        [Required]
        [Display(Name = "Item Name")]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Barcode / SKU")]
        public string Barcode { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Price")]
        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [Required]
        [Display(Name = "Current Stock")]
        public int StockLevel { get; set; }

        [Required]
        [Display(Name = "Reorder Level")]
        public int ReorderPoint { get; set; }

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}