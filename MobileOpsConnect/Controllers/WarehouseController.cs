using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Hubs;
using MobileOpsConnect.Models;
using MobileOpsConnect.Services;

namespace MobileOpsConnect.Controllers
{
    [Authorize(Roles = "WarehouseStaff,SuperAdmin,SystemAdmin,DepartmentManager")]
    public class WarehouseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IHubContext<InventoryHub> _hubContext;
        private readonly IEmailService _emailService;
        private readonly IAuditService _auditService;

        public WarehouseController(ApplicationDbContext context, INotificationService notificationService, IHubContext<InventoryHub> hubContext, IEmailService emailService, IAuditService auditService)
        {
            _context = context;
            _notificationService = notificationService;
            _hubContext = hubContext;
            _emailService = emailService;
            _auditService = auditService;
        }

        // GET: Warehouse/Index (The Scanner Screen)
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            var products = from p in _context.Products select p;

            if (!String.IsNullOrEmpty(searchString))
            {
                // Simulate "Scanning" by searching Name or SKU
                products = products.Where(s => s.Name.Contains(searchString) || s.SKU.Contains(searchString));
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
            product.LastUpdated = DateTime.Now;

            await _context.SaveChangesAsync();

            // Broadcast real-time update via SignalR
            await _hubContext.Clients.All.SendAsync("StockUpdated", product.ProductID, product.Name, product.StockQuantity, "Stock In");

            // Audit log
            var user = User.Identity;
            await _auditService.LogAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "", user?.Name ?? "", "", "STOCK_IN", $"Added {quantity} units to {product.Name} (SKU: {product.SKU}). New qty: {product.StockQuantity}.", HttpContext.Connection.RemoteIpAddress?.ToString());

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
            product.LastUpdated = DateTime.Now;

            await _context.SaveChangesAsync();

            // Broadcast real-time update via SignalR
            await _hubContext.Clients.All.SendAsync("StockUpdated", product.ProductID, product.Name, product.StockQuantity, "Stock Out");

            // Audit log
            await _auditService.LogAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "", User.Identity?.Name ?? "", "", "STOCK_OUT", $"Removed {quantity} units from {product.Name} (SKU: {product.SKU}). New qty: {product.StockQuantity}.", HttpContext.Connection.RemoteIpAddress?.ToString());

            // Check for low stock and notify
            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            int threshold = settings?.LowStockThreshold ?? 10;
            if (product.StockQuantity <= threshold)
            {
                var alertMessage = $"{product.Name} (SKU: {product.SKU}) is down to {product.StockQuantity} units after stock-out — below the {threshold}-unit threshold.";

                await _notificationService.SendToAllAsync(
                    "⚠️ Low Stock Alert", alertMessage);

                // Send email alert to support/admin
                var supportEmail = settings?.SupportEmail ?? "support@mobileops.com";
                await _emailService.SendEmailAsync(
                    supportEmail,
                    $"⚠️ Low Stock Alert: {product.Name}",
                    $"<h2>Low Stock Alert</h2><p>{alertMessage}</p><p>Please reorder this item as soon as possible.</p><hr><p><small>MobileOps Connect ERP — Automated Alert</small></p>");
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