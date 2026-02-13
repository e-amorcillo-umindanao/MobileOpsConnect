using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobileOpsConnect.Controllers
{
    [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
    public class OrdersController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ProcessOrder(int id, string actionType)
        {
            // Simulates processing an order and returning a success message
            TempData["Message"] = $"Purchase Order #{id} has been successfully {actionType.ToLower()}d.";
            return RedirectToAction(nameof(Index));
        }
    }
}