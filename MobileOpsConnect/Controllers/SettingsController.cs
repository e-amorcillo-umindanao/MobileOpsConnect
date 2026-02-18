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
        [Authorize(Roles = "SuperAdmin")]
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
    }
}