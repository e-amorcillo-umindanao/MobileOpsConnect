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
    }
}