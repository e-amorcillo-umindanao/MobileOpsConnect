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
        private readonly IAuditService _auditService;
        private readonly IInAppNotificationService _inAppNotificationService;
        private readonly HolidayService _holidayService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LeaveRequestsController> _logger;

        public LeaveRequestsController(ApplicationDbContext context, UserManager<IdentityUser> userManager, INotificationService notificationService, IInAppNotificationService inAppNotificationService, IHubContext<InventoryHub> hubContext, IAuditService auditService, HolidayService holidayService, IServiceScopeFactory scopeFactory, ILogger<LeaveRequestsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _inAppNotificationService = inAppNotificationService;
            _hubContext = hubContext;
            _auditService = auditService;
            _holidayService = holidayService;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // GET: LeaveRequests
        public async Task<IActionResult> Index(string? view, string? status, string? searchString, int? page)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var myRoles = await _userManager.GetRolesAsync(user);
            var myRole = myRoles.FirstOrDefault() ?? "";
            var subordinateRoles = GetSubordinateRoles(myRole);
            bool isMyView = string.Equals(view, "my", StringComparison.OrdinalIgnoreCase);
            
            // SECURITY: If not a boss and not "my" view, force "my" view
            if (!isMyView && subordinateRoles.Count == 0)
            {
                isMyView = true;
            }

            // Fix Data Placement issue: ensure Status always defaults to Active if not provided
            if (string.IsNullOrEmpty(status))
            {
                status = "Active";
            }

            ViewBag.IsMyView = isMyView;
            ViewBag.SelectedStatus = status;
            ViewBag.SearchString = searchString;

            IQueryable<LeaveRequest> query = _context.LeaveRequests.Include(l => l.User);

            if (isMyView)
            {
                query = query.Where(l => l.UserID == user.Id);
            }
            // If they have subordinate roles, we need to filter by those roles
            // Since roles are not in the DB directly (Identity), we still have to do some in-memory filtering
            // or use a join if possible. For now, we'll stick to the in-memory filtering of the scoped set
            // but we'll capture the counts for the metrics.

            var allScannedRequests = await query.ToListAsync();
            var filteredRequests = new List<LeaveRequest>();
            var userRoles = new Dictionary<string, string>();

            foreach (var req in allScannedRequests)
            {
                if (req.User == null) continue;
                
                if (!userRoles.ContainsKey(req.UserID))
                {
                    var roles = await _userManager.GetRolesAsync(req.User);
                    userRoles[req.UserID] = roles.FirstOrDefault() ?? "Employee";
                }

                string rRole = userRoles[req.UserID];

                // If not "my" view, check if user is a subordinate
                if (!isMyView && !subordinateRoles.Contains(rRole))
                {
                    continue;
                }

                // Apply Status Filter
                if (!string.IsNullOrEmpty(status) && status != "All")
                {
                    if (status == "Active" && req.Status != "Pending") continue;
                    if (status == "Archived" && req.Status == "Pending") continue;
                    if (status != "Active" && status != "Archived" && req.Status != status) continue;
                }

                // Apply Search Filter
                if (!string.IsNullOrEmpty(searchString))
                {
                    bool matchesEmail = req.User.Email != null && req.User.Email.Contains(searchString, StringComparison.OrdinalIgnoreCase);
                    bool matchesReason = req.Reason != null && req.Reason.Contains(searchString, StringComparison.OrdinalIgnoreCase);
                    bool matchesType = req.LeaveType != null && req.LeaveType.Contains(searchString, StringComparison.OrdinalIgnoreCase);
                    if (!matchesEmail && !matchesReason && !matchesType) continue;
                }

                filteredRequests.Add(req);
            }

            // Calculate metrics based on the scoped set (filtered by role/view but NOT by search/status/page)
            var metricsSet = isMyView ? allScannedRequests : allScannedRequests.Where(r => subordinateRoles.Contains(userRoles.GetValueOrDefault(r.UserID, ""))).ToList();
            ViewBag.TotalCount = metricsSet.Count;
            ViewBag.PendingCount = metricsSet.Count(l => l.Status == "Pending");
            ViewBag.ApprovedCount = metricsSet.Count(l => l.Status == "Approved");
            ViewBag.RejectedCount = metricsSet.Count(l => l.Status == "Rejected");
            ViewBag.UserRoles = userRoles;

            // Sort by DateRequested Descending
            var sortedRequests = filteredRequests.OrderByDescending(r => r.DateRequested).ToList();
            var paginatedList = PaginatedList<LeaveRequest>.Create(sortedRequests, page ?? 1, 10);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_LeaveRequestsTable", paginatedList);
            }

            return View(paginatedList);
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

            // SECURITY: Only the owner or a direct-tier superior can view details
            var user = await _userManager.GetUserAsync(User);
            var myRoles = await _userManager.GetRolesAsync(user);
            var myRole = myRoles.FirstOrDefault() ?? "";
            var subordinateRoles = GetSubordinateRoles(myRole);
            var requesterUser = await _userManager.FindByIdAsync(leaveRequest.UserID);
            var requesterRoles = requesterUser != null ? await _userManager.GetRolesAsync(requesterUser) : new List<string>();
            var requesterRole = requesterRoles.FirstOrDefault() ?? "Employee";
            bool canView = subordinateRoles.Contains(requesterRole) || leaveRequest.UserID == user?.Id;
            if (!canView)
            {
                return Forbid();
            }

            return View(leaveRequest);
        }

        // GET: LeaveRequests/Create
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var myLeaves = await _context.LeaveRequests
                    .Where(l => l.UserID == user.Id)
                    .OrderByDescending(l => l.DateRequested)
                    .ToListAsync();

                ViewBag.RecentLeaves = myLeaves.Take(10).ToList();
                ViewBag.MyTotalCount = myLeaves.Count;
                ViewBag.MyPendingCount = myLeaves.Count(l => l.Status == "Pending");
                ViewBag.MyApprovedCount = myLeaves.Count(l => l.Status == "Approved");
                ViewBag.MyRejectedCount = myLeaves.Count(l => l.Status == "Rejected");
            }

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

            // Server-side validation: StartDate must not be in the past (Philippine Time)
            if (leaveRequest.StartDate.Date < PhilippineTime.Today)
            {
                ModelState.AddModelError("StartDate", "Start date cannot be in the past.");
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();

                // Auto-fill system fields
                leaveRequest.UserID = user.Id;
                leaveRequest.DateRequested = PhilippineTime.Now;

                var roles = await _userManager.GetRolesAsync(user);
                var filerRole = roles.FirstOrDefault() ?? "Employee";

                // SuperAdmin leaves are auto-approved (no one above them)
                if (filerRole == "SuperAdmin")
                {
                    leaveRequest.Status = "Approved";
                    leaveRequest.ApprovedById = user.Id;
                }
                else
                {
                    leaveRequest.Status = "Pending";
                }

                _context.Add(leaveRequest);
                await _context.SaveChangesAsync();

                // Fire-and-forget: notifications are best-effort, don't block the response
                var leaveId = leaveRequest.LeaveID;
                var leaveType = leaveRequest.LeaveType;
                var startDate = leaveRequest.StartDate;
                var endDate = leaveRequest.EndDate;
                var userId = user.Id;
                var userEmail = user.Email!;
                var userIp = HttpContext.Connection.RemoteIpAddress?.ToString();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                        var inAppNotificationService = scope.ServiceProvider.GetRequiredService<IInAppNotificationService>();
                        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<InventoryHub>>();

                        if (filerRole != "SuperAdmin")
                        {
                            string targetRole = filerRole switch
                            {
                                "Employee" or "WarehouseStaff" => "DepartmentManager",
                                "DepartmentManager" => "SystemAdmin",
                                "SystemAdmin" => "SuperAdmin",
                                _ => "SuperAdmin"
                            };

                            await notificationService.SendToRoleAsync(targetRole,
                                "📋 New Leave Request",
                                $"{userEmail} submitted a {leaveType} leave request ({startDate:MMM dd} – {endDate:MMM dd}).");

                            // In-App Notification
                            await inAppNotificationService.CreateForRoleAsync(targetRole,
                                "📋 New Leave Request",
                                $"{userEmail} filed a {leaveType} leave.",
                                "Leave", "bi-calendar-plus", "/LeaveRequests/Index");

                            var displayName = userEmail.Split('@')[0];
                            await hubContext.Clients.Group($"role_{targetRole}").SendAsync("LeaveStatusChanged", leaveId, "Pending", userId, displayName);
                        }

                        await auditService.LogAsync(userId, userEmail, filerRole, "CREATE", $"Filed {leaveType} leave request ({startDate:MMM dd} – {endDate:MMM dd}).", userIp);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background notification failed for leave request #{LeaveId}", leaveId);
                    }
                });

                return RedirectToAction(nameof(Index), new { view = "my" });
            }
            // Re-populate holiday data for the view on validation failure
            var holidays = await _holidayService.GetHolidaysAsync("PH");
            ViewBag.Holidays = holidays;
            ViewBag.HolidaysJson = System.Text.Json.JsonSerializer.Serialize(
                holidays.Select(h => new { date = h.Date.ToString("yyyy-MM-dd"), name = h.LocalName, nameEn = h.Name }));
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

            // GUARD: Prevent double-approval
            if (leaveRequest.Status != "Pending")
            {
                TempData["Error"] = $"Leave request #{id} has already been {leaveRequest.Status.ToLower()}.";
                return RedirectToAction(nameof(Index));
            }

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

            // Fire-and-forget: notifications are best-effort
            var leaveId = leaveRequest.LeaveID;
            var leaveType = leaveRequest.LeaveType;
            var startDate = leaveRequest.StartDate;
            var endDate = leaveRequest.EndDate;
            var requesterId = leaveRequest.UserID;
            var approverId = approver.Id;
            var approverEmail = approver.Email!;
            var approverIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var inAppNotificationService = scope.ServiceProvider.GetRequiredService<IInAppNotificationService>();
                    var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                    var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<InventoryHub>>();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

                    var approveMsg = $"Your {leaveType} leave ({startDate:MMM dd} – {endDate:MMM dd}) has been approved.";
                    await notificationService.SendToUserAsync(requesterId, "✅ Leave Approved", approveMsg);

                    // In-App Notification
                    await inAppNotificationService.CreateAsync(requesterId,
                        "✅ Leave Approved",
                        $"Your {leaveType} leave request has been approved.",
                        "Leave", "bi-check-circle", "/LeaveRequests/Index?view=my");

                    await hubContext.Clients.User(requesterId).SendAsync("LeaveStatusChanged", leaveId, "Approved", requesterId, "");

                    var approverRoles = await userManager.GetRolesAsync(await userManager.FindByIdAsync(approverId));
                    await auditService.LogAsync(approverId, approverEmail, approverRoles.FirstOrDefault() ?? "", "APPROVE", $"Approved leave request #{leaveId} ({leaveType}).", approverIp);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background notification failed for leave approval #{LeaveId}", leaveId);
                }
            });

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

            // GUARD: Prevent double-rejection
            if (leaveRequest.Status != "Pending")
            {
                TempData["Error"] = $"Leave request #{id} has already been {leaveRequest.Status.ToLower()}.";
                return RedirectToAction(nameof(Index));
            }

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

            // Fire-and-forget: notifications are best-effort
            var leaveId = leaveRequest.LeaveID;
            var leaveType = leaveRequest.LeaveType;
            var startDate = leaveRequest.StartDate;
            var endDate = leaveRequest.EndDate;
            var requesterId = leaveRequest.UserID;
            var rejectorId = rejector.Id;
            var rejectorEmail = rejector.Email!;
            var rejectorIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var inAppNotificationService = scope.ServiceProvider.GetRequiredService<IInAppNotificationService>();
                    var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                    var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<InventoryHub>>();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

                    var rejectMsg = $"Your {leaveType} leave ({startDate:MMM dd} – {endDate:MMM dd}) has been rejected.";
                    await notificationService.SendToUserAsync(requesterId, "❌ Leave Rejected", rejectMsg);

                    // In-App Notification
                    await inAppNotificationService.CreateAsync(requesterId,
                        "❌ Leave Rejected",
                        $"Your {leaveType} leave request has been rejected.",
                        "Leave", "bi-x-circle", "/LeaveRequests/Index?view=my");

                    await hubContext.Clients.User(requesterId).SendAsync("LeaveStatusChanged", leaveId, "Rejected", requesterId, "");

                    var rejectorRoles = await userManager.GetRolesAsync(await userManager.FindByIdAsync(rejectorId));
                    await auditService.LogAsync(rejectorId, rejectorEmail, rejectorRoles.FirstOrDefault() ?? "", "REJECT", $"Rejected leave request #{leaveId} ({leaveType}).", rejectorIp);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background notification failed for leave rejection #{LeaveId}", leaveId);
                }
            });

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
                return RedirectToAction(nameof(Index), new { view = "my" });
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

            // SECURITY: Only the owner or a direct-tier superior can access delete
            var user = await _userManager.GetUserAsync(User);
            var myRoles = await _userManager.GetRolesAsync(user);
            var myRole = myRoles.FirstOrDefault() ?? "";
            var subordinateRoles = GetSubordinateRoles(myRole);
            var requesterUser = await _userManager.FindByIdAsync(leaveRequest.UserID);
            var requesterRoles = requesterUser != null ? await _userManager.GetRolesAsync(requesterUser) : new List<string>();
            var requesterRole = requesterRoles.FirstOrDefault() ?? "Employee";
            bool isBoss = subordinateRoles.Contains(requesterRole);
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

            // SECURITY: Only allow deletion of Pending requests
            if (leaveRequest.Status != "Pending") return Forbid();

            // SECURITY: Only the owner or a direct-tier superior can delete
            var user = await _userManager.GetUserAsync(User);
            var myRoles = await _userManager.GetRolesAsync(user);
            var myRole = myRoles.FirstOrDefault() ?? "";
            var subordinateRoles = GetSubordinateRoles(myRole);
            var requesterUser = await _userManager.FindByIdAsync(leaveRequest.UserID);
            var requesterRoles = requesterUser != null ? await _userManager.GetRolesAsync(requesterUser) : new List<string>();
            var requesterRole = requesterRoles.FirstOrDefault() ?? "Employee";
            bool isBoss = subordinateRoles.Contains(requesterRole);
            if (!isBoss && leaveRequest.UserID != user?.Id)
            {
                return Forbid();
            }

            // Delete FIRST, then notify (prevents race condition where notification fires but delete fails)
            _context.LeaveRequests.Remove(leaveRequest);
            await _context.SaveChangesAsync();

            // Audit log + notify up hierarchy (only after successful delete)
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var userRole = roles.FirstOrDefault() ?? "Employee";
                await _auditService.LogAsync(user.Id, user.Email!, userRole, "DELETE", $"Deleted leave request #{leaveRequest.LeaveID} ({leaveRequest.LeaveType}, {leaveRequest.StartDate:MMM dd} – {leaveRequest.EndDate:MMM dd}).", HttpContext.Connection.RemoteIpAddress?.ToString());

                string targetRole = userRole switch
                {
                    "Employee" or "WarehouseStaff" => "DepartmentManager",
                    "DepartmentManager" => "SystemAdmin",
                    "SystemAdmin" => "SuperAdmin",
                    _ => "SuperAdmin"
                };
                await _notificationService.SendToRoleAsync(targetRole,
                    "🗑️ Leave Request Cancelled",
                    $"{user.Email} cancelled their {leaveRequest.LeaveType} leave request ({leaveRequest.StartDate:MMM dd} – {leaveRequest.EndDate:MMM dd}).");

                // In-App Notification
                await _inAppNotificationService.CreateForRoleAsync(targetRole,
                    "🗑️ Leave Request Cancelled",
                    $"{user.Email} cancelled a {leaveRequest.LeaveType} leave.",
                    "Leave", "bi-calendar-x", "/LeaveRequests/Index");
            }

            return RedirectToAction(nameof(Index), new { view = "my" });
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

        /// <summary>
        /// Returns which roles' leave requests the given role can see and approve.
        /// Each role only manages the tier directly below.
        /// </summary>
        private static List<string> GetSubordinateRoles(string myRole) => myRole switch
        {
            "SuperAdmin"        => new() { "SystemAdmin" },
            "SystemAdmin"       => new() { "DepartmentManager" },
            "DepartmentManager" => new() { "WarehouseStaff", "Employee" },
            _                   => new()
        };
    }
}