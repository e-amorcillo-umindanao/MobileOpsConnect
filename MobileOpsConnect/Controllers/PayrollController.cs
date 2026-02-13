using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobileOpsConnect.Controllers
{
    [Authorize]
    public class PayrollController : Controller
    {
        public IActionResult MyPayslip()
        {
            return View();
        }
    }
}