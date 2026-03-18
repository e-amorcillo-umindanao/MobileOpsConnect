using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Models;  // <--- THIS LINE IS CRITICAL

namespace MobileOpsConnect.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; } // This needs the 'using' line above to work
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<UserFcmToken> UserFcmTokens { get; set; }
        public DbSet<UserPushSubscription> UserPushSubscriptions { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AccountingEntry> AccountingEntries { get; set; }
        public DbSet<InAppNotification> InAppNotifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Enforce SKU uniqueness to prevent duplicate barcode entries
            builder.Entity<Product>()
                .HasIndex(p => p.SKU)
                .IsUnique();

            builder.Entity<LeaveRequest>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_LeaveRequests_Status",
                    $"[Status] IN ('{LeaveRequestStatus.Pending}','{LeaveRequestStatus.Approved}','{LeaveRequestStatus.Rejected}')"));

            builder.Entity<PurchaseOrder>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_PurchaseOrders_Status",
                    $"[Status] IN ('{PurchaseOrderStatus.Pending}','{PurchaseOrderStatus.Approved}','{PurchaseOrderStatus.Rejected}')"));
        }
    }
}