using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;
using MobileOpsConnect.Services;

namespace MobileOpsConnect.Controllers
{
    [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
    public class AccountingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IAuditService _auditService;

        public AccountingController(ApplicationDbContext context, UserManager<IdentityUser> userManager, IAuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
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
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();
                entry.RecordedById = user.Id;
                entry.CreatedAt = DateTime.UtcNow;

                _context.Add(entry);
                await _context.SaveChangesAsync();

                // Audit Log
                var roles = await _userManager.GetRolesAsync(user);
                await _auditService.LogAsync(user.Id, user.Email!, roles.FirstOrDefault() ?? "", "CREATE_ACCOUNTING", 
                    $"Recorded {entry.Type}: {entry.Description} ({entry.Amount:C})", 
                    HttpContext.Connection.RemoteIpAddress?.ToString());

                TempData["Message"] = "Transaction recorded successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(entry);
        }
    }
}
