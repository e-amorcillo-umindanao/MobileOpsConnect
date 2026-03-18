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
        private readonly IInAppNotificationService _inAppNotificationService;

        public ProductsController(ApplicationDbContext context, INotificationService notificationService, IInAppNotificationService inAppNotificationService)
        {
            _context = context;
            _notificationService = notificationService;
            _inAppNotificationService = inAppNotificationService;
        }

        // GET: Products
        public async Task<IActionResult> Index(string? status, int? page)
        {
            status = ListStatusFilters.Normalize(status);
            var query = _context.Products.AsNoTracking();

            // Counts for tabs (full dataset)
            ViewBag.ActiveCount = await _context.Products.CountAsync(p => p.StockQuantity > 0);
            ViewBag.ArchivedCount = await _context.Products.CountAsync(p => p.StockQuantity == 0);
            ViewBag.CurrentStatus = status;
            ViewBag.ActiveTabValue = ListStatusFilters.Active;
            ViewBag.ArchivedTabValue = ListStatusFilters.Archived;

            // Simple metrics
            ViewBag.TotalProducts = ViewBag.ActiveCount + ViewBag.ArchivedCount;
            ViewBag.LowStockCount = await _context.Products.CountAsync(p => p.StockQuantity > 0 && p.StockQuantity <= 10);

            if (status == ListStatusFilters.Archived)
            {
                query = query.Where(p => p.StockQuantity == 0);
            }
            else
            {
                query = query.Where(p => p.StockQuantity > 0);
            }

            var products = query.OrderBy(p => p.Name);
            var paginatedList = await PaginatedList<Product>.CreateAsync(products, page ?? 1, 10);
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_ProductsTable", paginatedList);
            }

            return View(paginatedList);
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
        [Authorize(Roles = "SuperAdmin,DepartmentManager")]
        public async Task<IActionResult> Create()
        {
            await PopulateCategoriesAsync();
            return View();
        }

        private async Task PopulateCategoriesAsync()
        {
            var categories = new List<string>
            {
                "Computer Accessories",
                "Computers & Laptops",
                "Computer Components",
                "Networking Devices",
                "Storage Devices",
                "Printers & Scanners",
                "Power Products"
            };
            ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(categories);
        }

        // POST: Products/Create
        [Authorize(Roles = "SuperAdmin,DepartmentManager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductID,SKU,Name,Category,StockQuantity,Price")] Product product)
        {
            if (ModelState.IsValid)
            {
                product.LastUpdated = PhilippineTime.Now;
                _context.Add(product);
                await _context.SaveChangesAsync();

                // Check for low stock and notify
                await CheckAndNotifyLowStock(product);

                // Notify department managers about new product
                await _notificationService.SendToRoleAsync("DepartmentManager",
                    "📦 New Product Added",
                    $"{product.Name} (SKU: {product.SKU}) has been added with {product.StockQuantity} units.");

                // In-App Notification
                await _inAppNotificationService.CreateForRoleAsync("DepartmentManager",
                    "📦 New Product Added",
                    $"{product.Name} has been added with {product.StockQuantity} units.",
                    "Stock", "bi-plus-square", $"/Products/Details/{product.ProductID}");

                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: Products/Edit/5
        [Authorize(Roles = "SuperAdmin,DepartmentManager")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            await PopulateCategoriesAsync();
            return View(product);
        }

        // POST: Products/Edit/5
        [Authorize(Roles = "SuperAdmin,DepartmentManager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ProductID,SKU,Name,Category,StockQuantity,Price")] Product product)
        {
            if (id != product.ProductID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    product.LastUpdated = PhilippineTime.Now;
                    _context.Update(product);
                    await _context.SaveChangesAsync();

                    // Check for low stock and notify
                    await CheckAndNotifyLowStock(product);

                    // Notify department managers about product update
                    await _notificationService.SendToRoleAsync("DepartmentManager",
                        "✏️ Product Updated",
                        $"{product.Name} (SKU: {product.SKU}) has been updated. Stock: {product.StockQuantity}.");

                    // In-App Notification
                    await _inAppNotificationService.CreateForRoleAsync("DepartmentManager",
                        "✏️ Product Updated",
                        $"{product.Name} has been updated. Stock: {product.StockQuantity}.",
                        "Stock", "bi-pencil-square", $"/Products/Details/{product.ProductID}");
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
        [Authorize(Roles = "SuperAdmin,DepartmentManager")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FirstOrDefaultAsync(m => m.ProductID == id);
            if (product == null) return NotFound();

            return View(product);
        }

        // POST: Products/Delete/5 (Soft-delete: archives the product by setting stock to 0)
        [Authorize(Roles = "SuperAdmin,DepartmentManager")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                product.StockQuantity = 0;
                product.LastUpdated = PhilippineTime.Now;
                _context.Products.Update(product);
                await _context.SaveChangesAsync();

                // Notify department managers about archived product
                await _notificationService.SendToRoleAsync("DepartmentManager",
                    "🗂️ Product Archived",
                    $"{product.Name} (SKU: {product.SKU}) has been archived (stock set to 0).");

                // In-App Notification
                await _inAppNotificationService.CreateForRoleAsync("DepartmentManager",
                    "🗂️ Product Archived",
                    $"{product.Name} has been archived.",
                    "Stock", "bi-archive", $"/Products/Details/{product.ProductID}");
            }
            return RedirectToAction(nameof(Index));
        }

        // === Low Stock Notification Helper ===
        private async Task CheckAndNotifyLowStock(Product product)
        {
            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            int threshold = settings?.LowStockThreshold ?? 10;

            if (product.StockQuantity <= threshold)
            {
                await _notificationService.SendToRoleAsync("DepartmentManager",
                    "⚠️ Low Stock Alert",
                    $"{product.Name} (SKU: {product.SKU}) has only {product.StockQuantity} units left — below the {threshold}-unit threshold.");

                // In-App Notification
                await _inAppNotificationService.CreateForRoleAsync("DepartmentManager",
                    "⚠️ Low Stock Alert",
                    $"{product.Name} has only {product.StockQuantity} units left.",
                    "Stock", "bi-exclamation-triangle", $"/Warehouse/Adjust/{product.ProductID}");
            }
        }
    }
}