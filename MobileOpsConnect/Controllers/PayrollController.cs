using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Services;
using QuestPDF.Fluent;

namespace MobileOpsConnect.Controllers
{
    [Authorize]
    public class PayrollController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public PayrollController(UserManager<IdentityUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public IActionResult MyPayslip()
        {
            return View();
        }

        // GET: Payroll/DownloadPayslip
        public async Task<IActionResult> DownloadPayslip()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();

                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault() ?? "Employee";

                // Get company info from settings
                var settings = await _context.SystemSettings.FirstOrDefaultAsync();
                var companyName = settings?.CompanyName ?? "MobileOps Connect";

                // ────────────────────────────────────────────────
                // Role-based salary — EXACTLY matches MyPayslip.cshtml
                // TODO: Move salary configuration to database table or SystemSettings
                //       to avoid code changes when salary amounts are updated.
                // ────────────────────────────────────────────────
                decimal basicSalary = 22000m;
                decimal overtime    = 0m;
                decimal allowance   = 2000m;
                decimal tax         = 1500m;

                switch (role)
                {
                    case "SuperAdmin":
                        basicSalary = 150000m;
                        allowance   = 20000m;
                        tax         = 35000m;
                        break;
                    case "SystemAdmin":
                        basicSalary = 75000m;
                        allowance   = 5000m;
                        tax         = 12000m;
                        break;
                    case "DepartmentManager":
                        basicSalary = 55000m;
                        allowance   = 4000m;
                        tax         = 6500m;
                        break;
                    case "WarehouseStaff":
                        basicSalary = 26000m;
                        overtime    = 4500m;
                        allowance   = 1500m;
                        tax         = 2200m;
                        break;
                }

                // Standard deductions — matches view
                decimal sss        = 1350m;
                decimal philhealth = 1125m;
                decimal pagibig    = 100m;

                var payslipData = new PayslipData
                {
                    EmployeeName  = user.Email?.Split('@')[0] ?? "Employee",
                    EmployeeEmail = user.Email ?? "",
                    Role          = role,
                    CompanyName   = companyName,
                    PayPeriod     = $"{DateTime.Now:MMMM} 1 – {DateTime.Now:MMMM} {DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month)}, {DateTime.Now.Year}",
                    PayDate       = DateTime.Now,
                    BasicSalary   = basicSalary,
                    Overtime      = overtime,
                    Allowances    = allowance,
                    Tax           = tax,
                    SSS           = sss,
                    PhilHealth    = philhealth,
                    PagIbig       = pagibig
                };

                var document = new PayslipDocument(payslipData);
                var pdfBytes = document.GeneratePdf();

                return File(pdfBytes, "application/pdf", $"Payslip_{user.Email}_{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                // Log the error and show a user-friendly message
                Console.WriteLine($"[PayrollController] DownloadPayslip error: {ex}");
                TempData["Error"] = "Unable to generate PDF payslip. Please try again or contact support.";
                return RedirectToAction(nameof(MyPayslip));
            }
        }
    }
}