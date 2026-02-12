using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;

namespace MobileOpsConnect.Controllers
{
    [Authorize]
    public class LeaveRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public LeaveRequestsController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: LeaveRequests
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (User.IsInRole("DepartmentManager") || User.IsInRole("SuperAdmin"))
            {
                // Manager sees ALL requests
                return View(await _context.LeaveRequests.Include(l => l.User).ToListAsync());
            }
            else
            {
                // Employees see ONLY their own requests
                return View(await _context.LeaveRequests
                    .Where(l => l.UserID == user.Id)
                    .Include(l => l.User)
                    .ToListAsync());
            }
        }

        // GET: LeaveRequests/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: LeaveRequests/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("LeaveType,StartDate,EndDate,Reason")] LeaveRequest leaveRequest)
        {
            // 1. Get the current logged-in user
            var user = await _userManager.GetUserAsync(User);

            // 2. Fill in the hidden system fields
            leaveRequest.UserID = user.Id;
            leaveRequest.Status = "Pending";
            leaveRequest.DateRequested = DateTime.Now;

            // 3. Save to DB
            // (We skip full ModelState validation for User/UserID to keep it simple)
            _context.Add(leaveRequest);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}