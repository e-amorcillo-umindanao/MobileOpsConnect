using System.ComponentModel.DataAnnotations;

namespace MobileOpsConnect.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string UserEmail { get; set; } = string.Empty;

        [StringLength(50)]
        public string UserRole { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty; // LOGIN, CREATE, UPDATE, DELETE, STOCK_IN, STOCK_OUT, SECURITY, APPROVE, REJECT

        [StringLength(1000)]
        public string Details { get; set; } = string.Empty;

        [StringLength(45)]
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// If true, this event is a security/critical event.
        /// Beta (SystemAdmin) will NOT see critical events — only Alpha (SuperAdmin) can.
        /// </summary>
        public bool IsCritical { get; set; } = false;
    }
}
