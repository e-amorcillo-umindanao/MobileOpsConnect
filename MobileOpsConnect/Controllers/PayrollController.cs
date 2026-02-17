using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

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
            QuestPDF.Settings.License = LicenseType.Community;

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Employee";

            // Get company info from settings
            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            var companyName = settings?.CompanyName ?? "MobileOps Connect";
            var taxRate = settings?.TaxRate ?? 0.10m;

            // Role-based salary calculation (matches existing MyPayslip.cshtml logic)
            decimal baseSalary = role switch
            {
                "SuperAdmin" => 75000m,
                "SystemAdmin" => 55000m,
                "DepartmentManager" => 45000m,
                "WarehouseStaff" => 28000m,
                _ => 22000m
            };

            var payslipData = new PayslipData
            {
                EmployeeName = user.Email?.Split('@')[0] ?? "Employee",
                EmployeeEmail = user.Email ?? "",
                Role = role,
                CompanyName = companyName,
                PayPeriod = $"{DateTime.Now:MMMM 1} – {DateTime.Now:MMMM} {DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month)}, {DateTime.Now.Year}",
                PayDate = DateTime.Now,
                BasicSalary = baseSalary,
                Overtime = baseSalary * 0.05m,
                Allowances = 3000m,
                Tax = baseSalary * taxRate,
                SSS = 1125m,
                PhilHealth = 450m,
                PagIbig = 200m
            };

            var document = new PayslipDocument(payslipData);
            var pdfBytes = document.GeneratePdf();

            return File(pdfBytes, "application/pdf", $"Payslip_{user.Email}_{DateTime.Now:yyyyMMdd}.pdf");
        }
    }
}