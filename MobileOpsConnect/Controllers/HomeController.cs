using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;
using System.Diagnostics;

namespace MobileOpsConnect.Controllers
{
    [Authorize] // KEY: Forces everyone to log in first
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // === THE TRAFFIC COP LOGIC ===

            // 1. SuperAdmin (Alpha) & SystemAdmin (Beta) -> COMMAND CENTER
            if (User.IsInRole("SuperAdmin") || User.IsInRole("SystemAdmin"))
            {
                // Load Data for the Admin Cards
                ViewBag.TotalProducts = await _context.Products.CountAsync();
                ViewBag.LowStockCount = await _context.Products.Where(p => p.StockQuantity <= 10).CountAsync();
                ViewBag.TotalValue = await _context.Products.SumAsync(p => p.StockQuantity * p.Price);

                return View(); // Loads Views/Home/Index.cshtml
            }

            // 2. Department Manager (Charlie) -> MANAGER DASHBOARD
            else if (User.IsInRole("DepartmentManager"))
            {
                return View("ManagerDashboard"); // Loads Views/Home/ManagerDashboard.cshtml
            }

            // 3. Everyone Else (Echo, Delta) -> EMPLOYEE DASHBOARD
            else
            {
                return View("EmployeeDashboard"); // Loads Views/Home/EmployeeDashboard.cshtml
            }
        }

        // ADDED: Analytics Method for Managers and Admins
        [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
        public IActionResult Analytics()
        {
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