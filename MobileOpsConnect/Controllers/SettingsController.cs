using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;
using MobileOpsConnect.Services;

namespace MobileOpsConnect.Controllers
{
    // Only Admins can touch this
    [Authorize(Roles = "SuperAdmin,SystemAdmin")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly UserManager<IdentityUser> _userManager;

        public SettingsController(ApplicationDbContext context, IAuditService auditService, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _auditService = auditService;
            _userManager = userManager;
        }

        // GET: Settings
        public async Task<IActionResult> Index()
        {
            // 1. Try to get the existing settings (Row #1)
            var setting = await _context.SystemSettings.FirstOrDefaultAsync();

            // 2. If it doesn't exist yet (First run), create default
            if (setting == null)
            {
                setting = new SystemSetting
                {
                    CompanyName = "MobileOps Hardware",
                    SupportEmail = "help@mobileops.com",
                    LowStockThreshold = 10,
                    TaxRate = 12
                };
                _context.Add(setting);
                await _context.SaveChangesAsync();
            }

            return View(setting);
        }

        // POST: Settings/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CompanyName,SupportEmail,LowStockThreshold,TaxRate")] SystemSetting systemSetting)
        {
            if (id != systemSetting.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    systemSetting.LastUpdated = DateTime.UtcNow;
                    _context.Update(systemSetting);
                    await _context.SaveChangesAsync();
                    ViewBag.Message = "Settings updated successfully!";

                    // Audit log — settings change is a critical security event
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser == null) return Challenge();
                    var roles = await _userManager.GetRolesAsync(currentUser);
                    await _auditService.LogAsync(currentUser.Id, currentUser.Email!, roles.FirstOrDefault() ?? "", "SECURITY", $"Changed system settings (Company: {systemSetting.CompanyName}, Tax: {systemSetting.TaxRate}%, Threshold: {systemSetting.LowStockThreshold}).", HttpContext.Connection.RemoteIpAddress?.ToString(), isCritical: true);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.SystemSettings.Any(e => e.Id == systemSetting.Id)) return NotFound();
                    else throw;
                }
            }
            return View("Index", systemSetting);
        }

        // GET: Settings/AuditLogs
        [Authorize(Roles = "SuperAdmin,SystemAdmin")]
        public async Task<IActionResult> AuditLogs()
        {
            bool isSuperAdmin = User.IsInRole("SuperAdmin");

            // Alpha sees ALL logs; Beta sees non-critical only
            var query = _context.AuditLogs.AsQueryable();
            if (!isSuperAdmin)
            {
                query = query.Where(l => !l.IsCritical);
            }

            var logs = await query.OrderByDescending(l => l.Timestamp).Take(50).ToListAsync();

            ViewBag.IsSuperAdmin = isSuperAdmin;
            ViewBag.TotalEvents = logs.Count;
            ViewBag.LoginCount = logs.Count(l => l.Action == "LOGIN");
            ViewBag.CreateCount = logs.Count(l => l.Action == "CREATE" || l.Action == "UPDATE" || l.Action == "DELETE");
            ViewBag.StockCount = logs.Count(l => l.Action == "STOCK_IN" || l.Action == "STOCK_OUT");
            ViewBag.SecurityCount = logs.Count(l => l.IsCritical);

            return View(logs);
        }

        // --- NEW: System Maintenance & Database Backups ---

        // GET: Settings/Maintenance
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Maintenance()
        {
            return View();
        }

        // POST: Settings/ExportDatabase
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ExportDatabaseAsync()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            var exportData = new
            {
                Timestamp = DateTime.UtcNow,
                ExportedBy = User.Identity?.Name,
                Users = await _userManager.Users.Select(u => new { u.Id, u.Email, u.UserName, u.EmailConfirmed }).ToListAsync(),
                Roles = await _context.Roles.ToListAsync(),
                UserRoles = await _context.UserRoles.ToListAsync(),
                Products = await _context.Products.ToListAsync(),
                LeaveRequests = await _context.LeaveRequests.ToListAsync(),
                SystemSettings = await _context.SystemSettings.ToListAsync(),
                AccountingEntries = await _context.AccountingEntries.ToListAsync(),
                PurchaseOrders = await _context.PurchaseOrders.ToListAsync(),
                AuditLogs = await _context.AuditLogs.OrderByDescending(x => x.Timestamp).Take(1000).ToListAsync() // Limit to 1000 to prevent massive files
            };

            string json = JsonSerializer.Serialize(exportData, options);
            byte[] fileBytes = Encoding.UTF8.GetBytes(json);
            string fileName = $"MobileOps_Backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";

            // Log the backup action
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                await _auditService.LogAsync(currentUser.Id, currentUser.Email!, "SuperAdmin", "BACKUP", $"Generated full system JSON database backup ({fileBytes.Length / 1024} KB).", HttpContext.Connection.RemoteIpAddress?.ToString(), isCritical: true);
            }

            return File(fileBytes, "application/json", fileName);
        }

        // POST: Settings/PurgeAuditLogs
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> PurgeAuditLogsAsync()
        {
            var thresholdDate = DateTime.UtcNow.AddDays(-90); // Purge older than 90 days
            
            var oldLogs = await _context.AuditLogs.Where(l => l.Timestamp < thresholdDate).ToListAsync();
            int count = oldLogs.Count;

            if (count > 0)
            {
                _context.AuditLogs.RemoveRange(oldLogs);
                await _context.SaveChangesAsync();
                
                // Log the purge action
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _auditService.LogAsync(currentUser.Id, currentUser.Email!, "SuperAdmin", "MAINTENANCE", $"Purged {count} legacy audit logs older than 90 days.", HttpContext.Connection.RemoteIpAddress?.ToString(), isCritical: true);
                }
                
                TempData["MaintenanceSuccess"] = $"Successfully purged {count} legacy audit logs.";
            }
            else
            {
                TempData["MaintenanceInfo"] = "No legacy audit logs found older than 90 days. System is clean.";
            }

            return RedirectToAction(nameof(Maintenance));
        }

        // POST: Settings/PurgeAllData
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> PurgeAllDataAsync()
        {
            // Ensure accurate deletion order to prevent Foreign Key conflicts
            var purchaseOrders = await _context.PurchaseOrders.ToListAsync();
            _context.PurchaseOrders.RemoveRange(purchaseOrders);

            var accountingEntries = await _context.AccountingEntries.ToListAsync();
            _context.AccountingEntries.RemoveRange(accountingEntries);

            var leaveRequests = await _context.LeaveRequests.ToListAsync();
            _context.LeaveRequests.RemoveRange(leaveRequests);

            var products = await _context.Products.ToListAsync();
            _context.Products.RemoveRange(products);

            await _context.SaveChangesAsync();

            // Log the purge action
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                await _auditService.LogAsync(currentUser.Id, currentUser.Email!, "SuperAdmin", "MAINTENANCE", "Purged ALL accumulated transactional data (Products, Leaves, Orders, Accounting).", HttpContext.Connection.RemoteIpAddress?.ToString(), isCritical: true);
            }
            
            TempData["MaintenanceSuccess"] = "Successfully cleared all accumulated system data.";
            return RedirectToAction(nameof(Maintenance));
        }

    }
}