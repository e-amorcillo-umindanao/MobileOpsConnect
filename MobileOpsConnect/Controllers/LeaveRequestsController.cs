using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
            if (user == null) return Challenge();

            // LOGIC: Who sees what?
            // "Bosses" (SuperAdmin, SystemAdmin, DepartmentManager) see EVERYTHING.
            bool isBoss = User.IsInRole("SuperAdmin") ||
                          User.IsInRole("SystemAdmin") ||
                          User.IsInRole("DepartmentManager");

            if (isBoss)
            {
                // Show ALL requests (so Beta can approve Charlie, and Charlie can approve Echo)
                var allRequests = await _context.LeaveRequests.Include(l => l.User).ToListAsync();
                return View(allRequests);
            }
            else
            {
                // Regular employees only see their OWN history
                var myRequests = await _context.LeaveRequests
                    .Where(l => l.UserID == user.Id)
                    .Include(l => l.User)
                    .ToListAsync();
                return View(myRequests);
            }
        }

        // GET: LeaveRequests/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.LeaveRequests == null)
            {
                return NotFound();
            }

            var leaveRequest = await _context.LeaveRequests
                .Include(l => l.User)
                .FirstOrDefaultAsync(m => m.LeaveID == id);

            if (leaveRequest == null)
            {
                return NotFound();
            }

            return View(leaveRequest);
        }

        // GET: LeaveRequests/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: LeaveRequests/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("LeaveID,LeaveType,StartDate,EndDate,Reason")] LeaveRequest leaveRequest)
        {
            // Remove User and UserID from validation since we set them manually
            ModelState.Remove("User");
            ModelState.Remove("UserID");
            ModelState.Remove("Status");
            ModelState.Remove("DateRequested");

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);

                // Auto-fill system fields
                leaveRequest.UserID = user.Id;
                leaveRequest.Status = "Pending";
                leaveRequest.DateRequested = DateTime.Now;

                _context.Add(leaveRequest);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(leaveRequest);
        }

        // === APPROVAL ACTIONS (Only for Bosses) ===

        // GET: LeaveRequests/Approve/5
        [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
        public async Task<IActionResult> Approve(int id)
        {
            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest == null) return NotFound();

            leaveRequest.Status = "Approved";
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: LeaveRequests/Reject/5
        [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
        public async Task<IActionResult> Reject(int id)
        {
            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest == null) return NotFound();

            leaveRequest.Status = "Rejected";
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: LeaveRequests/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.LeaveRequests == null) return NotFound();

            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest == null) return NotFound();

            // Security: Only allow edit if it's still Pending
            if (leaveRequest.Status != "Pending") return Forbid();

            return View(leaveRequest);
        }

        // POST: LeaveRequests/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("LeaveID,UserID,LeaveType,StartDate,EndDate,Reason,Status,DateRequested")] LeaveRequest leaveRequest)
        {
            if (id != leaveRequest.LeaveID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(leaveRequest);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeaveRequestExists(leaveRequest.LeaveID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(leaveRequest);
        }

        // GET: LeaveRequests/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.LeaveRequests == null) return NotFound();

            var leaveRequest = await _context.LeaveRequests
                .Include(l => l.User)
                .FirstOrDefaultAsync(m => m.LeaveID == id);

            if (leaveRequest == null) return NotFound();

            return View(leaveRequest);
        }

        // POST: LeaveRequests/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.LeaveRequests == null) return Problem("Entity set 'ApplicationDbContext.LeaveRequests'  is null.");
            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest != null)
            {
                _context.LeaveRequests.Remove(leaveRequest);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool LeaveRequestExists(int id)
        {
            return (_context.LeaveRequests?.Any(e => e.LeaveID == id)).GetValueOrDefault();
        }
    }
}