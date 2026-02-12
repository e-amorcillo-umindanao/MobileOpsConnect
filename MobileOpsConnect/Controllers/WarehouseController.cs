using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;

namespace MobileOpsConnect.Controllers
{
    [Authorize(Roles = "WarehouseStaff,SuperAdmin,DepartmentManager")]
    public class WarehouseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WarehouseController(ApplicationDbContext context)
        {
            _context = context;
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

            // Get Low Stock Items for the Alert Box
            // (Assuming global threshold of 10, or use SystemSettings if available)
            ViewBag.LowStockItems = await _context.Products
                                          .Where(p => p.StockQuantity <= 10)
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
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            // LOGIC: Add to Stock
            product.StockQuantity += quantity;
            product.LastUpdated = DateTime.Now;

            // TODO: Add to Transaction History Table here (Optional for now)

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: Warehouse/StockOut (Remove Items)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockOut(int id, int quantity, string notes)
        {
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
            return RedirectToAction(nameof(Index));
        }
    }
}