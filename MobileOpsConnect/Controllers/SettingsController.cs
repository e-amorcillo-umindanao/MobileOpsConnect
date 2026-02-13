using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;

namespace MobileOpsConnect.Controllers
{
    // Only Admins can touch this
    [Authorize(Roles = "SuperAdmin,SystemAdmin")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SettingsController(ApplicationDbContext context)
        {
            _context = context;
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
                    systemSetting.LastUpdated = DateTime.Now;
                    _context.Update(systemSetting);
                    await _context.SaveChangesAsync();
                    ViewBag.Message = "Settings updated successfully!";
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
        public IActionResult AuditLogs()
        {
            return View();
        }
    }
}