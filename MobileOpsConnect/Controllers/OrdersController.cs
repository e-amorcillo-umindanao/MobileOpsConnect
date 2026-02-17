using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;
using MobileOpsConnect.Services;

namespace MobileOpsConnect.Controllers
{
    [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager,WarehouseStaff")]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IAuditService _auditService;
        private readonly UserManager<IdentityUser> _userManager;

        public OrdersController(ApplicationDbContext context, INotificationService notificationService, IAuditService auditService, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _notificationService = notificationService;
            _auditService = auditService;
            _userManager = userManager;
        }

        // GET: Orders
        public async Task<IActionResult> Index()
        {
            var orders = await _context.PurchaseOrders
                .Include(po => po.Product)
                .Include(po => po.RequestedBy)
                .OrderByDescending(po => po.DateRequested)
                .ToListAsync();

            // Warehouse staff only sees their own POs
            if (User.IsInRole("WarehouseStaff"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                orders = orders.Where(po => po.RequestedById == currentUser!.Id).ToList();
            }

            ViewBag.CanApprove = User.IsInRole("SuperAdmin") || User.IsInRole("SystemAdmin") || User.IsInRole("DepartmentManager");
            return View(orders);
        }

        // GET: Orders/Create
        [Authorize(Roles = "WarehouseStaff")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Products = new SelectList(await _context.Products.OrderBy(p => p.Name).ToListAsync(), "ProductID", "Name");
            return View();
        }

        // POST: Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "WarehouseStaff")]
        public async Task<IActionResult> Create(int ProductId, int Quantity, string? Notes)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var product = await _context.Products.FindAsync(ProductId);
            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction(nameof(Create));
            }

            var order = new PurchaseOrder
            {
                ProductId = ProductId,
                Quantity = Quantity,
                EstimatedCost = product.Price * Quantity,
                RequestedById = currentUser.Id,
                Status = "Pending",
                DateRequested = DateTime.Now,
                Notes = Notes
            };

            _context.PurchaseOrders.Add(order);
            await _context.SaveChangesAsync();

            // Notify managers about new PO
            await _notificationService.SendToAllAsync(
                "📦 New Purchase Order",
                $"{currentUser.Email} submitted PO for {Quantity}x {product.Name} (₱{order.EstimatedCost:N0}).");

            // Audit log
            await _auditService.LogAsync(currentUser.Id, currentUser.Email!, "WarehouseStaff", "CREATE", $"Submitted purchase order #{order.Id} for {Quantity}x {product.Name}.", HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["Message"] = $"Purchase Order #{order.Id} submitted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Orders/ProcessOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
        public async Task<IActionResult> ProcessOrder(int id, string actionType)
        {
            var order = await _context.PurchaseOrders.Include(po => po.Product).FirstOrDefaultAsync(po => po.Id == id);
            if (order == null) return NotFound();

            if (order.Status != "Pending")
            {
                TempData["Error"] = $"Purchase Order #{id} has already been {order.Status.ToLower()}.";
                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var action = actionType?.Trim() ?? "Approve";
            var isApproval = action.Equals("Approve", StringComparison.OrdinalIgnoreCase);

            order.Status = isApproval ? "Approved" : "Rejected";
            order.ApprovedById = currentUser!.Id;
            order.DateProcessed = DateTime.Now;

            await _context.SaveChangesAsync();

            var pastTense = isApproval ? "approved" : "rejected";

            // Notify all users
            await _notificationService.SendToAllAsync(
                "📦 Order Update",
                $"Purchase Order #{id} has been {pastTense} by {currentUser.Email}.");

            // Audit log
            var roles = await _userManager.GetRolesAsync(currentUser);
            await _auditService.LogAsync(currentUser.Id, currentUser.Email!, roles.FirstOrDefault() ?? "", isApproval ? "APPROVE" : "REJECT", $"{(isApproval ? "Approved" : "Rejected")} purchase order #{id} ({order.Product?.Name}).", HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["Message"] = $"Purchase Order #{id} has been successfully {pastTense}.";
            return RedirectToAction(nameof(Index));
        }
    }
}