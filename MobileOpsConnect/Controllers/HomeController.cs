using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;
using System.Diagnostics;

namespace MobileOpsConnect.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        // Store application start time to calculate uptime
        private static readonly DateTime _appStartTime = DateTime.UtcNow;

        public HomeController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // ── Shared data ──
            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            int threshold = settings?.LowStockThreshold ?? 10;

            var totalProducts = await _context.Products.CountAsync();
            var lowStockCount = await _context.Products.Where(p => p.StockQuantity <= threshold).CountAsync();
            var totalValue = await _context.Products.SumAsync(p => (decimal?)p.StockQuantity * p.Price) ?? 0m;
            var pendingLeaves = await _context.LeaveRequests.CountAsync(l => l.Status == "Pending");

            // On leave today: approved leaves where today falls within start–end range
            var today = DateTime.Today;
            var onLeaveToday = await _context.LeaveRequests
                .CountAsync(l => l.Status == "Approved" && l.StartDate <= today && l.EndDate >= today);

            // ═══════════════════════════════════════════
            // 1. SuperAdmin (Alpha) & SystemAdmin (Beta)
            // ═══════════════════════════════════════════
            if (User.IsInRole("SuperAdmin") || User.IsInRole("SystemAdmin"))
            {
                // ── Business overview (already existed) ──
                ViewBag.TotalProducts = totalProducts;
                ViewBag.LowStockCount = lowStockCount;
                ViewBag.TotalValue = totalValue;

                // ── System health ──
                ViewBag.ActiveUsers = await _userManager.Users.CountAsync();
                ViewBag.PendingLeaves = pendingLeaves;
                ViewBag.OnLeaveToday = onLeaveToday;

                // DB size — query SQL Server system view
                try
                {
                    var dbSizeResult = await _context.Database
                        .SqlQueryRaw<decimal>("SELECT CAST(SUM(size) * 8.0 / 1024 AS decimal(18,2)) AS [Value] FROM sys.database_files")
                        .FirstOrDefaultAsync();
                    ViewBag.DbSizeMB = dbSizeResult;
                }
                catch
                {
                    ViewBag.DbSizeMB = (decimal?)null; // Permission denied or unsupported
                }

                // Uptime — time since this application instance started
                var uptime = DateTime.UtcNow - _appStartTime;
                if (uptime.TotalDays >= 1)
                    ViewBag.UptimeDisplay = $"{(int)uptime.TotalDays}d {uptime.Hours}h";
                else if (uptime.TotalHours >= 1)
                    ViewBag.UptimeDisplay = $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
                else
                    ViewBag.UptimeDisplay = $"{uptime.Minutes}m {uptime.Seconds}s";

                ViewBag.AppStartTimeUtc = _appStartTime.ToString("o");

                return View();
            }

            // ═══════════════════════════════════════════
            // 2. Department Manager (Charlie)
            // ═══════════════════════════════════════════
            else if (User.IsInRole("DepartmentManager"))
            {
                // Exclude SuperAdmin from team count
                var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");
                var allUsers = await _userManager.Users.CountAsync();
                ViewBag.TeamCount = allUsers - superAdmins.Count;

                ViewBag.PendingLeaves = pendingLeaves;
                ViewBag.OnLeaveToday = onLeaveToday;
                ViewBag.TotalProducts = totalProducts;
                ViewBag.LowStockCount = lowStockCount;

                // In-stock percentage
                if (totalProducts > 0)
                {
                    var inStockPercent = ((totalProducts - lowStockCount) * 100) / totalProducts;
                    ViewBag.InStockPercent = inStockPercent;
                }
                else
                {
                    ViewBag.InStockPercent = 0;
                }

                // Oldest pending leave
                var oldestPending = await _context.LeaveRequests
                    .Where(l => l.Status == "Pending")
                    .OrderBy(l => l.DateRequested)
                    .Select(l => l.DateRequested)
                    .FirstOrDefaultAsync();

                if (oldestPending != default)
                {
                    var daysAgo = (DateTime.Now - oldestPending).Days;
                    ViewBag.OldestPendingDays = daysAgo == 0 ? "Today" : $"{daysAgo} day{(daysAgo == 1 ? "" : "s")} ago";
                }
                else
                {
                    ViewBag.OldestPendingDays = "None";
                }

                // On leave percentage for progress bar
                var teamCount = (int)ViewBag.TeamCount;
                ViewBag.OnLeavePercent = teamCount > 0 ? (onLeaveToday * 100) / teamCount : 0;

                // Pending purchase orders for approval
                ViewBag.PendingOrders = await _context.PurchaseOrders.CountAsync(po => po.Status == "Pending");

                return View("ManagerDashboard");
            }

            // ═══════════════════════════════════════════
            // 3. Everyone Else (Echo, Delta)
            // ═══════════════════════════════════════════
            else
            {
                var user = await _userManager.GetUserAsync(User);
                var userId = user?.Id ?? "";

                // ── Warehouse (Delta) metrics ──
                ViewBag.TotalProducts = totalProducts;
                ViewBag.LowStockCount = lowStockCount;

                // ── Employee (Echo) leave metrics ──
                var userApproved = await _context.LeaveRequests
                    .CountAsync(l => l.UserID == userId && l.Status == "Approved");
                var userPending = await _context.LeaveRequests
                    .CountAsync(l => l.UserID == userId && l.Status == "Pending");

                // Calculate total leave days used (sum of days per approved leave)
                var approvedLeaves = await _context.LeaveRequests
                    .Where(l => l.UserID == userId && l.Status == "Approved")
                    .Select(l => new { l.StartDate, l.EndDate })
                    .ToListAsync();
                var totalLeaveDaysUsed = approvedLeaves.Sum(l => (l.EndDate - l.StartDate).Days + 1);

                var totalLeaveAllowance = 15; // Standard 15 days per year
                var leaveBalance = totalLeaveAllowance - totalLeaveDaysUsed;
                if (leaveBalance < 0) leaveBalance = 0;

                ViewBag.LeaveBalance = leaveBalance;
                ViewBag.LeavesUsed = totalLeaveDaysUsed;
                ViewBag.LeaveAllowance = totalLeaveAllowance;
                ViewBag.LeaveUsedPercent = totalLeaveAllowance > 0 ? (totalLeaveDaysUsed * 100) / totalLeaveAllowance : 0;
                ViewBag.UserPending = userPending;
                ViewBag.UserApproved = userApproved;

                // Next pay date = end of current month
                var nextPayDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 
                    DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));
                ViewBag.NextPayDate = nextPayDate.ToString("MMM dd");

                // Latest payslip period
                ViewBag.LatestPayPeriod = $"{DateTime.Now:MMM} 1 – {DateTime.Now:MMM} 15, {DateTime.Now.Year}";

                // Pending POs for Delta (WarehouseStaff)
                if (User.IsInRole("WarehouseStaff"))
                {
                    var userId2 = user?.Id ?? "";
                    ViewBag.MyPendingOrders = await _context.PurchaseOrders.CountAsync(po => po.RequestedById == userId2 && po.Status == "Pending");
                }

                return View("EmployeeDashboard");
            }
        }

        [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
        public async Task<IActionResult> Analytics()
        {
            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            int threshold = settings?.LowStockThreshold ?? 10;

            // Inventory data
            var totalProducts = await _context.Products.CountAsync();
            var totalValue = await _context.Products.SumAsync(p => (decimal?)p.StockQuantity * p.Price) ?? 0m;
            var lowStockCount = await _context.Products.Where(p => p.StockQuantity <= threshold).CountAsync();

            ViewBag.TotalProducts = totalProducts;
            ViewBag.TotalValue = totalValue;
            ViewBag.LowStockCount = lowStockCount;

            // Leave utilization
            var today = DateTime.Today;
            var allUsers = await _userManager.Users.CountAsync();
            var onLeaveToday = await _context.LeaveRequests
                .CountAsync(l => l.Status == "Approved" && l.StartDate <= today && l.EndDate >= today);
            ViewBag.LeaveUtilPercent = allUsers > 0 ? (onLeaveToday * 100) / allUsers : 0;
            ViewBag.OnLeaveCount = onLeaveToday;

            // Top categories by product count
            var categories = await _context.Products
                .GroupBy(p => p.Category)
                .Select(g => new { Category = g.Key, Count = g.Count(), Value = g.Sum(p => p.StockQuantity * p.Price) })
                .OrderByDescending(c => c.Value)
                .Take(4)
                .ToListAsync();

            ViewBag.TopCategories = categories.Select(c => new {
                c.Category,
                c.Count,
                Percent = totalProducts > 0 ? (c.Count * 100) / totalProducts : 0
            }).ToList();

            // Recent low-stock alerts (actual product names)
            var lowStockProducts = await _context.Products
                .Where(p => p.StockQuantity <= threshold)
                .OrderBy(p => p.StockQuantity)
                .Take(3)
                .Select(p => new { p.Name, p.StockQuantity })
                .ToListAsync();
            ViewBag.LowStockAlerts = lowStockProducts;

            // Pending leaves
            var pendingLeaves = await _context.LeaveRequests
                .Where(l => l.Status == "Pending")
                .CountAsync();
            ViewBag.PendingLeaves = pendingLeaves;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}