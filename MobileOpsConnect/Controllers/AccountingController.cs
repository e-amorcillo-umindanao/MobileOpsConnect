using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;
using MobileOpsConnect.Services;

namespace MobileOpsConnect.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class AccountingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IAuditService _auditService;
        private readonly ExchangeRateService _exchangeRateService;
        private readonly INotificationService _notificationService;
        private readonly IInAppNotificationService _inAppNotificationService;

        public AccountingController(ApplicationDbContext context, UserManager<IdentityUser> userManager, IAuditService auditService, ExchangeRateService exchangeRateService, INotificationService notificationService, IInAppNotificationService inAppNotificationService)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
            _exchangeRateService = exchangeRateService;
            _notificationService = notificationService;
            _inAppNotificationService = inAppNotificationService;
        }

        // GET: Accounting
        public async Task<IActionResult> Index(string filterType, string filterCategory, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.AccountingEntries
                .Include(a => a.RecordedBy)
                .Include(a => a.PurchaseOrder)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filterType))
            {
                query = query.Where(a => a.Type == filterType);
            }

            if (!string.IsNullOrEmpty(filterCategory))
            {
                query = query.Where(a => a.Category.Contains(filterCategory));
            }

            if (startDate.HasValue)
            {
                query = query.Where(a => a.TransactionDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.TransactionDate <= endDate.Value);
            }

            var entries = await query.OrderByDescending(a => a.TransactionDate).ToListAsync();

            // Calculate totals
            ViewBag.TotalIncome = entries.Where(a => a.Type == "Income").Sum(a => a.Amount);
            ViewBag.TotalExpense = entries.Where(a => a.Type == "Expense").Sum(a => a.Amount);
            ViewBag.NetResult = ViewBag.TotalIncome - ViewBag.TotalExpense;

            // Fetch live exchange rates from ExchangeRate-API
            var exchangeRates = await _exchangeRateService.GetRatesAsync("PHP");
            ViewBag.ExchangeRates = exchangeRates;

            return View(entries);
        }

        // GET: Accounting/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var entry = await _context.AccountingEntries
                .Include(a => a.RecordedBy)
                .Include(a => a.PurchaseOrder)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (entry == null) return NotFound();

            return View(entry);
        }

        // GET: Accounting/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Accounting/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TransactionDate,Type,Category,Description,Amount,ReferenceNumber,Notes")] AccountingEntry entry)
        {
            // Remove server-set fields from validation (set manually below)
            ModelState.Remove("RecordedById");
            ModelState.Remove("RecordedBy");

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();
                entry.RecordedById = user.Id;
                entry.CreatedAt = PhilippineTime.Now;

                _context.Add(entry);
                await _context.SaveChangesAsync();

                // Audit Log
                var roles = await _userManager.GetRolesAsync(user!);
                await _auditService.LogAsync(user.Id, user.Email!, roles.FirstOrDefault() ?? "", "CREATE_ACCOUNTING", 
                    $"Recorded {entry.Type}: {entry.Description} ({entry.Amount:C})", 
                    HttpContext.Connection.RemoteIpAddress?.ToString());

                // Push notification to SuperAdmin
                await _notificationService.SendToRoleAsync("SuperAdmin",
                    "💰 New Transaction",
                    $"{entry.Type}: {entry.Description} (₱{entry.Amount:N2})");

                // In-App Notification
                await _inAppNotificationService.CreateForRoleAsync("SuperAdmin",
                    "💰 New Transaction",
                    $"{entry.Type}: {entry.Description} (₱{entry.Amount:N2})",
                    "Accounting", "bi-cash-coin", "/Accounting/Index");

                TempData["Message"] = "Transaction recorded successfully!";
                return RedirectToAction(nameof(Invoice), new { id = entry.Id });
            }
            return View(entry);
        }

        // GET: Accounting/Invoice/5
        public async Task<IActionResult> Invoice(int? id)
        {
            if (id == null) return NotFound();

            var entry = await _context.AccountingEntries
                .Include(a => a.RecordedBy)
                .Include(a => a.PurchaseOrder)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (entry == null) return NotFound();

            return View(entry);
        }
    }
}
