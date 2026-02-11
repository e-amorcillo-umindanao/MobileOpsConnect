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
            // 2. TRAFFIC COP: Identify who is logging in.
            // If the email address contains "staff", send them to the Staff Portal.
            // (e.g., staff@mobileops.com)
            if (User.Identity != null && User.Identity.Name != null && User.Identity.Name.Contains("staff"))
            {
                return RedirectToAction("EmployeeDashboard");
            }

            // 3. ADMIN LOGIC: If it's NOT a staff member, show the Admin Dashboard.
            // We fetch real data from the database here.
            ViewBag.TotalProducts = _context.Product.Count();
            ViewBag.LowStock = _context.Product.Count(p => p.StockLevel < p.ReorderPoint);
            ViewBag.TotalValue = _context.Product.Sum(p => p.StockLevel * p.UnitPrice);

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