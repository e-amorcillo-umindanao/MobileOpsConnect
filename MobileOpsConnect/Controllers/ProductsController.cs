using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;
using MobileOpsConnect.Services;

namespace MobileOpsConnect.Controllers
{
    [Authorize] // Require login
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public ProductsController(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // GET: Products
        public async Task<IActionResult> Index()
        {
            return View(await _context.Products.ToListAsync());
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FirstOrDefaultAsync(m => m.ProductID == id);
            if (product == null) return NotFound();

            return View(product);
        }

        // GET: Products/Create
        [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Products/Create
        [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductID,SKU,Name,Category,StockQuantity,Price")] Product product)
        {
            if (ModelState.IsValid)
            {
                product.LastUpdated = DateTime.Now;
                _context.Add(product);
                await _context.SaveChangesAsync();

                // Check for low stock and notify
                await CheckAndNotifyLowStock(product);

                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: Products/Edit/5
        [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            return View(product);
        }

        // POST: Products/Edit/5
        [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ProductID,SKU,Name,Category,StockQuantity,Price")] Product product)
        {
            if (id != product.ProductID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    product.LastUpdated = DateTime.Now;
                    _context.Update(product);
                    await _context.SaveChangesAsync();

                    // Check for low stock and notify
                    await CheckAndNotifyLowStock(product);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Products.Any(e => e.ProductID == product.ProductID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: Products/Delete/5
        [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FirstOrDefaultAsync(m => m.ProductID == id);
            if (product == null) return NotFound();

            return View(product);
        }

        // POST: Products/Delete/5
        [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // === Low Stock Notification Helper ===
        private async Task CheckAndNotifyLowStock(Product product)
        {
            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            int threshold = settings?.LowStockThreshold ?? 10;

            if (product.StockQuantity <= threshold)
            {
                await _notificationService.SendToAllAsync(
                    "⚠️ Low Stock Alert",
                    $"{product.Name} (SKU: {product.SKU}) has only {product.StockQuantity} units left — below the {threshold}-unit threshold.");
            }
        }
    }
}