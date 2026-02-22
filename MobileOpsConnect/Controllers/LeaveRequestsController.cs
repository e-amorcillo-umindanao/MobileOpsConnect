using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Hubs;
using MobileOpsConnect.Models;
using MobileOpsConnect.Services;

namespace MobileOpsConnect.Controllers
{
    [Authorize]
    public class LeaveRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly INotificationService _notificationService;
        private readonly IHubContext<InventoryHub> _hubContext;
        private readonly IEmailService _emailService;
        private readonly IAuditService _auditService;
        private readonly HolidayService _holidayService;

        public LeaveRequestsController(ApplicationDbContext context, UserManager<IdentityUser> userManager, INotificationService notificationService, IHubContext<InventoryHub> hubContext, IEmailService emailService, IAuditService auditService, HolidayService holidayService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _hubContext = hubContext;
            _emailService = emailService;
            _auditService = auditService;
            _holidayService = holidayService;
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

            List<LeaveRequest> requests;

            if (isBoss)
            {
                // Show ALL requests (so Beta can approve Charlie, and Charlie can approve Echo)
                requests = await _context.LeaveRequests.Include(l => l.User).ToListAsync();
            }
            else
            {
                // Regular employees only see their OWN history
                requests = await _context.LeaveRequests
                    .Where(l => l.UserID == user.Id)
                    .Include(l => l.User)
                    .ToListAsync();
            }

            // Pre-load roles for all users to avoid N+1 queries in the view
            var userRoles = new Dictionary<string, string>();
            foreach (var request in requests)
            {
                if (request.User != null && !userRoles.ContainsKey(request.UserID))
                {
                    var roles = await _userManager.GetRolesAsync(request.User);
                    userRoles[request.UserID] = roles.FirstOrDefault() ?? "Employee";
                }
            }
            ViewBag.UserRoles = userRoles;

            return View(requests);
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

            // SECURITY: Only the owner or a boss can view details
            var user = await _userManager.GetUserAsync(User);
            bool isBoss = User.IsInRole("SuperAdmin") ||
                          User.IsInRole("SystemAdmin") ||
                          User.IsInRole("DepartmentManager");
            if (!isBoss && leaveRequest.UserID != user?.Id)
            {
                return Forbid();
            }

            return View(leaveRequest);
        }

        // GET: LeaveRequests/Create
        public async Task<IActionResult> Create()
        {
            // Fetch Philippine public holidays from Nager.Date API
            var holidays = await _holidayService.GetHolidaysAsync("PH");
            ViewBag.Holidays = holidays;
            // Serialize for client-side JavaScript usage
            ViewBag.HolidaysJson = System.Text.Json.JsonSerializer.Serialize(
                holidays.Select(h => new { date = h.Date.ToString("yyyy-MM-dd"), name = h.LocalName, nameEn = h.Name }));
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

            // Server-side validation: EndDate must be on or after StartDate
            if (leaveRequest.EndDate < leaveRequest.StartDate)
            {
                ModelState.AddModelError("EndDate", "End date must be on or after the start date.");
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();

                // Auto-fill system fields
                leaveRequest.UserID = user.Id;
                leaveRequest.Status = "Pending";
                leaveRequest.DateRequested = DateTime.UtcNow;

                _context.Add(leaveRequest);
                await _context.SaveChangesAsync();

                // Notify admins about the new leave request
                await _notificationService.SendToAllAsync(
                    "📋 New Leave Request",
                    $"{user.Email} submitted a {leaveRequest.LeaveType} leave request ({leaveRequest.StartDate:MMM dd} – {leaveRequest.EndDate:MMM dd}).");

                // Broadcast real-time update via SignalR
                await _hubContext.Clients.All.SendAsync("LeaveStatusChanged", leaveRequest.LeaveID, "Pending", leaveRequest.UserID);

                // Audit log
                var roles = await _userManager.GetRolesAsync(user);
                await _auditService.LogAsync(user.Id, user.Email!, roles.FirstOrDefault() ?? "", "CREATE", $"Filed {leaveRequest.LeaveType} leave request ({leaveRequest.StartDate:MMM dd} – {leaveRequest.EndDate:MMM dd}).", HttpContext.Connection.RemoteIpAddress?.ToString());

                return RedirectToAction(nameof(Index));
            }
            return View(leaveRequest);
        }

        // === APPROVAL ACTIONS (Only for Bosses, POST only) ===

        // POST: LeaveRequests/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
        public async Task<IActionResult> Approve(int id)
        {
            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest == null) return NotFound();

            var approver = await _userManager.GetUserAsync(User);
            if (approver == null) return Challenge();

            // HIERARCHY: Approver rank must be strictly higher than requester rank
            var requester = await _userManager.FindByIdAsync(leaveRequest.UserID);
            if (requester == null) return NotFound();
            int approverRank = await GetRank(approver);
            int requesterRank = await GetRank(requester);
            if (approverRank <= requesterRank) return Forbid();

            leaveRequest.Status = "Approved";
            leaveRequest.ApprovedById = approver.Id;
            await _context.SaveChangesAsync();

            // Notify the employee via push + email
            var approveMsg = $"Your {leaveRequest.LeaveType} leave ({leaveRequest.StartDate:MMM dd} – {leaveRequest.EndDate:MMM dd}) has been approved.";
            await _notificationService.SendToUserAsync(leaveRequest.UserID, "✅ Leave Approved", approveMsg);

            var employee = await _userManager.FindByIdAsync(leaveRequest.UserID);
            if (employee?.Email != null)
            {
                await _emailService.SendEmailAsync(employee.Email, "✅ Leave Approved",
                    $"<h2>Leave Approved</h2><p>{approveMsg}</p><hr><p><small>MobileOps Connect ERP</small></p>");
            }

            // Broadcast real-time update via SignalR
            await _hubContext.Clients.All.SendAsync("LeaveStatusChanged", leaveRequest.LeaveID, "Approved", leaveRequest.UserID);

            // Audit log
            var approverRoles = await _userManager.GetRolesAsync(approver);
            await _auditService.LogAsync(approver.Id, approver.Email!, approverRoles.FirstOrDefault() ?? "", "APPROVE", $"Approved leave request #{id} ({leaveRequest.LeaveType}).", HttpContext.Connection.RemoteIpAddress?.ToString());

            return RedirectToAction(nameof(Index));
        }

        // POST: LeaveRequests/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
        public async Task<IActionResult> Reject(int id)
        {
            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest == null) return NotFound();

            var rejector = await _userManager.GetUserAsync(User);
            if (rejector == null) return Challenge();

            // HIERARCHY: Rejector rank must be strictly higher than requester rank
            var requester = await _userManager.FindByIdAsync(leaveRequest.UserID);
            if (requester == null) return NotFound();
            int rejectorRank = await GetRank(rejector);
            int requesterRank = await GetRank(requester);
            if (rejectorRank <= requesterRank) return Forbid();

            leaveRequest.Status = "Rejected";
            leaveRequest.ApprovedById = rejector.Id;
            await _context.SaveChangesAsync();

            // Notify the employee via push + email
            var rejectMsg = $"Your {leaveRequest.LeaveType} leave ({leaveRequest.StartDate:MMM dd} – {leaveRequest.EndDate:MMM dd}) has been rejected.";
            await _notificationService.SendToUserAsync(leaveRequest.UserID, "❌ Leave Rejected", rejectMsg);

            var rejectedEmployee = await _userManager.FindByIdAsync(leaveRequest.UserID);
            if (rejectedEmployee?.Email != null)
            {
                await _emailService.SendEmailAsync(rejectedEmployee.Email, "❌ Leave Rejected",
                    $"<h2>Leave Rejected</h2><p>{rejectMsg}</p><hr><p><small>MobileOps Connect ERP</small></p>");
            }

            // Broadcast real-time update via SignalR
            await _hubContext.Clients.All.SendAsync("LeaveStatusChanged", leaveRequest.LeaveID, "Rejected", leaveRequest.UserID);

            // Audit log
            var rejectorRoles = await _userManager.GetRolesAsync(rejector);
            await _auditService.LogAsync(rejector.Id, rejector.Email!, rejectorRoles.FirstOrDefault() ?? "", "REJECT", $"Rejected leave request #{id} ({leaveRequest.LeaveType}).", HttpContext.Connection.RemoteIpAddress?.ToString());

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

            // Security: Only the owner can edit their own request
            var currentUser = await _userManager.GetUserAsync(User);
            if (leaveRequest.UserID != currentUser?.Id) return Forbid();

            return View(leaveRequest);
        }

        // POST: LeaveRequests/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("LeaveID,LeaveType,StartDate,EndDate,Reason")] LeaveRequest formData)
        {
            if (id != formData.LeaveID) return NotFound();

            // Load the real record from DB to preserve sensitive fields
            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest == null) return NotFound();

            // SECURITY: Re-check that the request is still "Pending"
            if (leaveRequest.Status != "Pending") return Forbid();

            // SECURITY: Only the owner can edit their own request
            var currentUser = await _userManager.GetUserAsync(User);
            if (leaveRequest.UserID != currentUser?.Id) return Forbid();

            // Remove navigation/system fields from validation
            ModelState.Remove("User");
            ModelState.Remove("UserID");
            ModelState.Remove("Status");
            ModelState.Remove("DateRequested");

            if (ModelState.IsValid)
            {
                try
                {
                    // Only update safe fields
                    leaveRequest.LeaveType = formData.LeaveType;
                    leaveRequest.StartDate = formData.StartDate;
                    leaveRequest.EndDate = formData.EndDate;
                    leaveRequest.Reason = formData.Reason;

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

            // SECURITY: Only the owner or a boss can access delete
            var user = await _userManager.GetUserAsync(User);
            bool isBoss = User.IsInRole("SuperAdmin") ||
                          User.IsInRole("SystemAdmin") ||
                          User.IsInRole("DepartmentManager");
            if (!isBoss && leaveRequest.UserID != user?.Id)
            {
                return Forbid();
            }

            return View(leaveRequest);
        }

        // POST: LeaveRequests/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.LeaveRequests == null) return Problem("Entity set 'ApplicationDbContext.LeaveRequests'  is null.");
            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest == null) return NotFound();

            // SECURITY: Only the owner or a boss can delete
            var user = await _userManager.GetUserAsync(User);
            bool isBoss = User.IsInRole("SuperAdmin") ||
                          User.IsInRole("SystemAdmin") ||
                          User.IsInRole("DepartmentManager");
            if (!isBoss && leaveRequest.UserID != user?.Id)
            {
                return Forbid();
            }

            _context.LeaveRequests.Remove(leaveRequest);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool LeaveRequestExists(int id)
        {
            return (_context.LeaveRequests?.Any(e => e.LeaveID == id)).GetValueOrDefault();
        }

        // HIERARCHY HELPER: Maps a user to a rank integer.
        // SuperAdmin=4, SystemAdmin=3, DepartmentManager=2, Everyone else=1
        private async Task<int> GetRank(IdentityUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "";
            return role switch
            {
                "SuperAdmin" => 4,
                "SystemAdmin" => 3,
                "DepartmentManager" => 2,
                _ => 1
            };
        }
    }
}