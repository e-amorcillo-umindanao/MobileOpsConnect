using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;
using System.Diagnostics;

namespace MobileOpsConnect.Controllers
{
    // 1. SECURITY LOCK: This protects the entire Dashboard.
    // If you are not logged in, you will be kicked to the Login Page.
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // 1. ROUTING: Redirect Staff & Employees to the Staff Portal
            if (User.IsInRole("WarehouseStaff") || User.IsInRole("Employee"))
            {
                return RedirectToAction("EmployeeDashboard");
            }

            // 2. ROUTING: Redirect Department Manager to their specific Dashboard
            if (User.IsInRole("DepartmentManager"))
            {
                return RedirectToAction("ManagerDashboard");
            }

            // 3. ADMIN DASHBOARD: For SuperAdmin & SystemAdmin
            // Fetch high-level stats for the "Command Center"
            ViewBag.TotalProducts = _context.Product.Count();
            ViewBag.LowStock = _context.Product.Count(p => p.StockLevel < p.ReorderPoint);
            ViewBag.TotalValue = _context.Product.Sum(p => p.StockLevel * p.UnitPrice);

            return View();
        }

        // === NEW ACTION ===
        [Authorize(Roles = "DepartmentManager,SuperAdmin")] // SuperAdmin can peek too
        public IActionResult ManagerDashboard()
        {
            // In a real app, you would fetch "Pending Leave Requests" count here
            ViewBag.PendingLeaves = 0; 
            ViewBag.PendingOrders = 0; 
            return View();
        }

        public IActionResult EmployeeDashboard()
        {
            // 4. STAFF PORTAL: This is the view for Warehouse employees.
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}