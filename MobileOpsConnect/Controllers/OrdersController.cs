using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using MobileOpsConnect.Data;
using MobileOpsConnect.Hubs;
using MobileOpsConnect.Models;
using MobileOpsConnect.Services;

namespace MobileOpsConnect.Controllers
{
    [Authorize(Roles = "DepartmentManager,WarehouseStaff")]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IInAppNotificationService _inAppNotificationService;
        private readonly IAuditService _auditService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IHubContext<InventoryHub> _hubContext;

        public OrdersController(ApplicationDbContext context, INotificationService notificationService, IInAppNotificationService inAppNotificationService, IAuditService auditService, UserManager<IdentityUser> userManager, IHubContext<InventoryHub> hubContext)
        {
            _context = context;
            _notificationService = notificationService;
            _inAppNotificationService = inAppNotificationService;
            _auditService = auditService;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        // GET: Orders
        public async Task<IActionResult> Index(string? status, int? page)
        {
            var query = _context.PurchaseOrders
                .Include(po => po.Product)
                .Include(po => po.RequestedBy)
                .AsQueryable();

            // Calculate counts for summary
            ViewBag.TotalCount = await query.CountAsync();
            ViewBag.PendingCount = await query.CountAsync(po => po.Status == "Pending");
            ViewBag.ApprovedCount = await query.CountAsync(po => po.Status == "Approved");
            ViewBag.RejectedCount = await query.CountAsync(po => po.Status == "Rejected");

            string currentStatus = status ?? "active";
            ViewBag.CurrentStatus = currentStatus;

            if (currentStatus == "active")
            {
                query = query.Where(po => po.Status == "Pending");
            }
            else if (currentStatus == "archived")
            {
                query = query.Where(po => po.Status != "Pending");
            }

            var orders = query.OrderByDescending(po => po.DateRequested);

            ViewBag.CanApprove = User.IsInRole("DepartmentManager") || User.IsInRole("SuperAdmin");
            var paginatedList = await PaginatedList<PurchaseOrder>.CreateAsync(orders.AsNoTracking(), page ?? 1, 10);
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_OrdersTable", paginatedList);
            }

            return View(paginatedList);
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
                DateRequested = PhilippineTime.Now,
                Notes = Notes
            };

            _context.PurchaseOrders.Add(order);
            await _context.SaveChangesAsync();

            // Notify department managers about new PO (Push)
            await _notificationService.SendToRoleAsync("DepartmentManager",
                "📦 New Purchase Order",
                $"{currentUser.Email} submitted PO for {Quantity}x {product.Name} (₱{order.EstimatedCost:N0}).");

            // In-App Notification
            await _inAppNotificationService.CreateForRoleAsync("DepartmentManager",
                "📦 New Purchase Order",
                $"{currentUser.Email} submitted a PO for {Quantity}x {product.Name}.",
                "Order", "bi-bag-plus", $"/Orders/Index");

            // Real-time broadcast (SignalR)
            await _hubContext.Clients.Group("role_DepartmentManager")
                .SendAsync("PurchaseOrderUpdated", order.Id, product.Name, Quantity, "Submitted", currentUser.Email);

            // Audit log
            var roles = await _userManager.GetRolesAsync(currentUser);
            await _auditService.LogAsync(currentUser.Id, currentUser.Email!, roles.FirstOrDefault() ?? "", "CREATE", $"Submitted purchase order #{order.Id} for {Quantity}x {product.Name}.", HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["Message"] = $"Purchase Order #{order.Id} submitted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Orders/ProcessOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DepartmentManager")]
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
            if (currentUser == null) return Challenge();
            var action = actionType?.Trim() ?? "Approve";
            var isApproval = action.Equals("Approve", StringComparison.OrdinalIgnoreCase);

            order.Status = isApproval ? "Approved" : "Rejected";
            order.ApprovedById = currentUser.Id;
            order.DateProcessed = PhilippineTime.Now;

            await _context.SaveChangesAsync();

            var pastTense = isApproval ? "approved" : "rejected";

            // Notify the requester about the decision
            await _notificationService.SendToUserAsync(order.RequestedById,
                isApproval ? "✅ Order Approved" : "❌ Order Rejected",
                $"Purchase Order #{id} ({order.Product?.Name}) has been {pastTense} by {currentUser.Email}.");

            // In-App Notification
            await _inAppNotificationService.CreateAsync(order.RequestedById,
                isApproval ? "✅ Order Approved" : "❌ Order Rejected",
                $"Your PO for {order.Product?.Name} has been {pastTense}.",
                "Order", isApproval ? "bi-check-circle" : "bi-x-circle", "/Orders/Index");

            // If approved, notify warehouse staff about incoming stock
            if (isApproval)
            {
                await _notificationService.SendToRoleAsync("WarehouseStaff",
                    "📦 PO Approved — Incoming Stock",
                    $"PO #{id}: {order.Quantity}x {order.Product?.Name} approved. Prepare for receiving.");

                // In-App Notification
                await _inAppNotificationService.CreateForRoleAsync("WarehouseStaff",
                    "📦 PO Approved",
                    $"{order.Quantity}x {order.Product?.Name} approved. Prepare for receiving.",
                    "Order", "bi-truck", "/Warehouse/Index");
            }

            // Audit log
            var roles = await _userManager.GetRolesAsync(currentUser);
            await _auditService.LogAsync(currentUser.Id, currentUser.Email!, roles.FirstOrDefault() ?? "", isApproval ? "APPROVE" : "REJECT", $"{(isApproval ? "Approved" : "Rejected")} purchase order #{id} ({order.Product?.Name}).", HttpContext.Connection.RemoteIpAddress?.ToString());

            // Real-time broadcast (SignalR)
            await _hubContext.Clients.Group("role_WarehouseStaff")
                .SendAsync("PurchaseOrderUpdated", id, order.Product?.Name, order.Quantity, isApproval ? "Approved" : "Rejected", currentUser.Email);
            await _hubContext.Clients.Group("role_DepartmentManager")
                .SendAsync("PurchaseOrderUpdated", id, order.Product?.Name, order.Quantity, isApproval ? "Approved" : "Rejected", currentUser.Email);

            TempData["Message"] = $"Purchase Order #{id} has been successfully {pastTense}.";
            return RedirectToAction(nameof(Index));
        }
    }
}