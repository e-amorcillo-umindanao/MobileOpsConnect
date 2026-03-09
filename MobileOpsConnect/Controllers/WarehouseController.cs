using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Hubs;
using MobileOpsConnect.Models;
using MobileOpsConnect.Services;

namespace MobileOpsConnect.Controllers
{
    [Authorize(Roles = "WarehouseStaff")]
    public class WarehouseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IInAppNotificationService _inAppNotificationService;
        private readonly IHubContext<InventoryHub> _hubContext;
        private readonly IAuditService _auditService;
        private readonly UserManager<IdentityUser> _userManager;

        public WarehouseController(ApplicationDbContext context, INotificationService notificationService, IInAppNotificationService inAppNotificationService, IHubContext<InventoryHub> hubContext, IAuditService auditService, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _notificationService = notificationService;
            _inAppNotificationService = inAppNotificationService;
            _hubContext = hubContext;
            _auditService = auditService;
            _userManager = userManager;
        }

        // GET: Warehouse/Index (The Scanner Screen)
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            var products = from p in _context.Products select p;

            if (!String.IsNullOrEmpty(searchString))
            {
                // Priority: exact SKU match (barcode scan), then fuzzy fallback (manual search)
                var exactMatch = products.Where(s => s.SKU == searchString);
                if (await exactMatch.AnyAsync())
                {
                    products = exactMatch;
                }
                else
                {
                    products = products.Where(s => s.Name.Contains(searchString) || s.SKU.Contains(searchString));
                }
            }

            // Get configurable threshold from settings
            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            int threshold = settings?.LowStockThreshold ?? 10;

            // Get Low Stock Items for the Alert Box
            ViewBag.LowStockItems = await _context.Products
                                          .Where(p => p.StockQuantity <= threshold)
                                          .ToListAsync();

            return View(await products.ToListAsync());
        }

        // GET: Warehouse/Adjust/5
        public async Task<IActionResult> Adjust(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            return View(product);
        }

        // POST: Warehouse/StockIn (Add Items)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockIn(int id, int quantity, string notes)
        {
            if (quantity < 1)
            {
                TempData["Error"] = "Quantity must be at least 1.";
                return RedirectToAction("Adjust", new { id = id });
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            // LOGIC: Add to Stock
            product.StockQuantity += quantity;
            product.LastUpdated = PhilippineTime.Now;

            await _context.SaveChangesAsync();

            // Broadcast real-time update via SignalR
            await _hubContext.Clients.All.SendAsync("StockUpdated", product.ProductID, product.Name, product.StockQuantity, "Stock In");

            // Push notification to DeptManager
            var currentUser = await _userManager.GetUserAsync(User);
            await _notificationService.SendToRoleAsync("DepartmentManager",
                "📥 Stock In",
                $"{currentUser?.Email} added {quantity} units of {product.Name} (SKU: {product.SKU}). New qty: {product.StockQuantity}.");

            // In-App Notification
            await _inAppNotificationService.CreateForRoleAsync("DepartmentManager",
                "📥 Stock In",
                $"{currentUser?.Email} added {quantity} units of {product.Name}.",
                "Stock", "bi-box-arrow-in-down", $"/Warehouse/Adjust/{product.ProductID}");

            // Audit log
            var userId = currentUser?.Id ?? "";
            var userEmail = currentUser?.Email ?? "";
            var userRoles = currentUser != null ? await _userManager.GetRolesAsync(currentUser) : new List<string>();
            var userRole = userRoles.FirstOrDefault() ?? "";
            var logMessage = $"Added {quantity} units to {product.Name} (SKU: {product.SKU}). New qty: {product.StockQuantity}.";
            if (!string.IsNullOrWhiteSpace(notes)) logMessage += $" Notes: {notes}";
            await _auditService.LogAsync(userId, userEmail, userRole, "STOCK_IN", logMessage, HttpContext.Connection.RemoteIpAddress?.ToString());

            return RedirectToAction(nameof(Index));
        }

        // POST: Warehouse/StockOut (Remove Items)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockOut(int id, int quantity, string notes)
        {
            if (quantity < 1)
            {
                TempData["Error"] = "Quantity must be at least 1.";
                return RedirectToAction("Adjust", new { id = id });
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            // LOGIC: Remove from Stock (Prevent negative)
            if (product.StockQuantity < quantity)
            {
                TempData["Error"] = "Insufficient stock!";
                return RedirectToAction("Adjust", new { id = id });
            }

            product.StockQuantity -= quantity;
            product.LastUpdated = PhilippineTime.Now;

            await _context.SaveChangesAsync();

            // Broadcast real-time update via SignalR
            await _hubContext.Clients.All.SendAsync("StockUpdated", product.ProductID, product.Name, product.StockQuantity, "Stock Out");

            // Push notification to DeptManager
            var currentUser = await _userManager.GetUserAsync(User);
            await _notificationService.SendToRoleAsync("DepartmentManager",
                "📤 Stock Out",
                $"{currentUser?.Email} removed {quantity} units of {product.Name} (SKU: {product.SKU}). New qty: {product.StockQuantity}.");

            // In-App Notification
            await _inAppNotificationService.CreateForRoleAsync("DepartmentManager",
                "📤 Stock Out",
                $"{currentUser?.Email} removed {quantity} units of {product.Name}.",
                "Stock", "bi-box-arrow-up", $"/Warehouse/Adjust/{product.ProductID}");

            // Audit log
            var userId = currentUser?.Id ?? "";
            var userEmail = currentUser?.Email ?? "";
            var userRoles = currentUser != null ? await _userManager.GetRolesAsync(currentUser) : new List<string>();
            var userRole = userRoles.FirstOrDefault() ?? "";
            var logMessage = $"Removed {quantity} units from {product.Name} (SKU: {product.SKU}). New qty: {product.StockQuantity}.";
            if (!string.IsNullOrWhiteSpace(notes)) logMessage += $" Notes: {notes}";
            await _auditService.LogAsync(userId, userEmail, userRole, "STOCK_OUT", logMessage, HttpContext.Connection.RemoteIpAddress?.ToString());

            // Check for low stock and notify
            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            int threshold = settings?.LowStockThreshold ?? 10;
            if (product.StockQuantity <= threshold)
            {
                var alertMessage = $"{product.Name} (SKU: {product.SKU}) is down to {product.StockQuantity} units after stock-out — below the {threshold}-unit threshold.";

                await _notificationService.SendToRoleAsync("DepartmentManager",
                    "⚠️ Low Stock Alert", alertMessage);

                // In-App Notification
                await _inAppNotificationService.CreateForRoleAsync("DepartmentManager",
                    "⚠️ Low Stock Alert",
                    $"{product.Name} is down to {product.StockQuantity} units.",
                    "Stock", "bi-exclamation-triangle", $"/Warehouse/Adjust/{product.ProductID}");
            }

            return RedirectToAction(nameof(Index));
        }

        // NEW: GET: Warehouse/LowStock
        public async Task<IActionResult> LowStock()
        {
            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            int threshold = settings?.LowStockThreshold ?? 10;

            var lowStockItems = await _context.Products
                                          .Where(p => p.StockQuantity <= threshold)
                                          .ToListAsync();
            return View(lowStockItems);
        }
    }
}