using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileOpsConnect.Services;

namespace MobileOpsConnect.Controllers
{
    [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
    public class OrdersController : Controller
    {
        private readonly INotificationService _notificationService;

        public OrdersController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessOrder(int id, string actionType)
        {
            // Null-safe with proper past tense
            var action = actionType?.Trim().ToLower() ?? "process";
            var pastTense = action.EndsWith("e") ? action + "d" : action + "ed";
            TempData["Message"] = $"Purchase Order #{id} has been successfully {pastTense}.";

            // Notify all users about the order action
            await _notificationService.SendToAllAsync(
                "📦 Order Update",
                $"Purchase Order #{id} has been {pastTense}.");

            return RedirectToAction(nameof(Index));
        }
    }
}