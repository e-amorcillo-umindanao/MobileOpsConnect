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
        [ValidateAntiForgeryToken]
        public IActionResult ProcessOrder(int id, string actionType)
        {
            // Null-safe with proper past tense
            var action = actionType?.Trim().ToLower() ?? "process";
            var pastTense = action.EndsWith("e") ? action + "d" : action + "ed";
            TempData["Message"] = $"Purchase Order #{id} has been successfully {pastTense}.";
            return RedirectToAction(nameof(Index));
        }
    }
}