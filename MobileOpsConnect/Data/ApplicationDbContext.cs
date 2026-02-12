using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Models;

namespace MobileOpsConnect.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
    {
        public DbSet<MobileOpsConnect.Models.Product> Product { get; set; } = default!;

        public DbSet<LeaveRequest> LeaveRequests { get; set; }
    }
}
