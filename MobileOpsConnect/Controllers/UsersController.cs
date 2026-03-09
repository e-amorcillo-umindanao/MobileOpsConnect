using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Data;
using MobileOpsConnect.Models;
using MobileOpsConnect.Services;


namespace MobileOpsConnect.Controllers
{
    // REVERTED: Only SuperAdmin and SystemAdmin can access this. 
    // DepartmentManager handles Leaves, not User Accounts.
    [Authorize(Roles = "SuperAdmin,SystemAdmin,DepartmentManager")]
    public class UsersController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IAuditService _auditService;
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IInAppNotificationService _inAppNotificationService;

        public UsersController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<IdentityUser> signInManager,
            IAuditService auditService,
            ApplicationDbContext context,
            INotificationService notificationService,
            IInAppNotificationService inAppNotificationService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _auditService = auditService;
            _context = context;
            _notificationService = notificationService;
            _inAppNotificationService = inAppNotificationService;
        }

        // GET: Users
        [Authorize(Roles = "SuperAdmin,SystemAdmin")]
        public async Task<IActionResult> Index(string? role, string? searchString, int? page)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var allUsers = await _userManager.Users.ToListAsync();
            var userViewModels = new List<UserViewModel>();

            foreach (var user in allUsers)
            {
                if (user == null) continue;
                var roles = await _userManager.GetRolesAsync(user);
                var userRole = roles.FirstOrDefault() ?? "Employee";

                // SCOPE LOGIC: SuperAdmin manages SystemAdmins; SystemAdmin manages Managers/Staff.
                if (CanManageUser(currentUser, userRole, user.Id))
                {
                    // Filter by role if specified
                    bool matchesRole = string.IsNullOrEmpty(role) || userRole == role;
                    
                    // Filter by search string (Email or Role)
                    bool matchesSearch = string.IsNullOrEmpty(searchString) || 
                                       (user.Email != null && user.Email.Contains(searchString, StringComparison.OrdinalIgnoreCase)) ||
                                       userRole.Contains(searchString, StringComparison.OrdinalIgnoreCase);

                    if (matchesRole && matchesSearch)
                    {
                        userViewModels.Add(new UserViewModel
                        {
                            Id = user.Id,
                            Email = user.Email,
                            Role = userRole
                        });
                    }
                }
            }

            // Define hierarchical order
            var roleOrder = new Dictionary<string, int>
            {
                { "SuperAdmin", 1 }, { "SystemAdmin", 2 }, { "DepartmentManager", 3 },
                { "WarehouseStaff", 4 }, { "Employee", 5 }
            };

            // Sort hierarchically
            var sortedViewModels = userViewModels
                .OrderBy(u => roleOrder.ContainsKey(u.Role) ? roleOrder[u.Role] : 99)
                .ThenBy(u => u.Email)
                .ToList();

            // Prepare role filter list (hierarchical)
            var availableRoles = new List<string>();
            if (User.IsInRole("SuperAdmin"))
            {
                // SuperAdmin scope: themselves + SystemAdmins only
                availableRoles.Add("SuperAdmin");
                availableRoles.Add("SystemAdmin");
            }
            else if (User.IsInRole("SystemAdmin"))
            {
                // SystemAdmin scope: Managers, Staff, Employees
                availableRoles.Add("DepartmentManager");
                availableRoles.Add("WarehouseStaff");
                availableRoles.Add("Employee");
            }

            ViewBag.AllRoles = availableRoles;
            ViewBag.SelectedRole = role;
            ViewBag.SearchString = searchString;
            ViewBag.RoleDisplayNames = new Dictionary<string, string>
            {
                { "SuperAdmin", "Super Admin" }, { "SystemAdmin", "System Admin" },
                { "DepartmentManager", "Department Manager" }, { "WarehouseStaff", "Warehouse Staff" },
                { "Employee", "Employee" }
            };

            // Calculate global counts for the entire directory (unfiltered within scope)
            ViewBag.TotalUsers = userViewModels.Count; 
            ViewBag.SuperAdminCount = userViewModels.Count(u => u.Role == "SuperAdmin");
            ViewBag.SystemAdminCount = userViewModels.Count(u => u.Role == "SystemAdmin");

            return View(PaginatedList<UserViewModel>.Create(sortedViewModels, page ?? 1, 10));
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            // Fix: Pass ViewBag.IsSuperAdmin for the View's logic
            ViewBag.IsSuperAdmin = User.IsInRole("SuperAdmin");

            // Fix: Pass the roles for the dropdown
            ViewBag.Roles = GetAllowedRolesSelectList();
            return View();
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Note: Using parameters to match your existing Create.cshtml <input name="...">
        public async Task<IActionResult> Create(string email, string password, string role)
        {
            // 1. SECURITY: Prevent Privilege Escalation (The Crash Fix)
            if (!IsRoleAllowed(role))
            {
                ModelState.AddModelError("", "You are not authorized to assign this role.");
                ViewBag.IsSuperAdmin = User.IsInRole("SuperAdmin");
                ViewBag.Roles = GetAllowedRolesSelectList();
                return View();
            }

            // 2. Create User
            var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Challenge();
                var currentRoles = await _userManager.GetRolesAsync(currentUser);
                await _auditService.LogAsync(currentUser.Id, currentUser.Email!, currentRoles.FirstOrDefault() ?? "", "CREATE", $"Created user account: {email} with role {role}.", HttpContext.Connection.RemoteIpAddress?.ToString());

                // Push notification to SuperAdmin
                await _notificationService.SendToRoleAsync("SuperAdmin",
                    "👤 New User Created",
                    $"{email} has been added as {role} by {currentUser.Email}.");

                // In-App Notification
                await _inAppNotificationService.CreateForRoleAsync("SuperAdmin",
                    "👤 New User Created",
                    $"{email} added as {role}.",
                    "User", "bi-person-plus", "/Users/Index");

                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            ViewBag.IsSuperAdmin = User.IsInRole("SuperAdmin");
            ViewBag.Roles = GetAllowedRolesSelectList();
            return View();
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user!);
            string currentRole = roles.FirstOrDefault() ?? "Employee";

            bool isOwnAccount = (user.Id == currentUser.Id);

            // SECURITY: Prevent accessing someone outside your scope
            if (!CanManageUser(currentUser, currentRole, user.Id))
            {
                return Forbid();
            }

            var model = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email,
                CurrentRole = currentRole,
                NewRole = currentRole,
                IsOwnAccount = isOwnAccount
            };

            ViewBag.IsAlpha = User.IsInRole("SuperAdmin");
            ViewBag.IsEditingOwnAccount = isOwnAccount;
            // When Alpha edits another user, include SuperAdmin as a promotion option
            ViewBag.Roles = User.IsInRole("SuperAdmin") && !isOwnAccount
                ? GetAllowedRolesForEditSelectList()
                : GetAllowedRolesSelectList();
            return View(model);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, EditUserViewModel model)
        {
            if (id != model.Id) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            bool isOwnAccount = (user.Id == currentUser.Id);

            // 1. RE-CHECK PERMISSIONS (Security Fix)
            var roles = await _userManager.GetRolesAsync(user!);
            string currentDbRole = roles.FirstOrDefault() ?? "Employee";

            if (!CanManageUser(currentUser, currentDbRole, user.Id)) return Forbid();

            // 2. PREVENT SELF-ROLE-CHANGE (Alpha's edit page has no dropdown)
            if (isOwnAccount)
            {
                return RedirectToAction(nameof(Index));
            }

            // 3. VALIDATE NEW ROLE (Privilege Escalation Fix)
            // Alpha can assign SuperAdmin when editing (promotion), so check edit list
            bool roleAllowed = User.IsInRole("SuperAdmin")
                ? GetAllowedRolesForEditList().Contains(model.NewRole)
                : IsRoleAllowed(model.NewRole);
            if (!roleAllowed) return Forbid();

            if (ModelState.IsValid)
            {
                // Logic: Only update role if it changed
                if (model.NewRole != currentDbRole)
                {
                    await _userManager.RemoveFromRoleAsync(user, currentDbRole);
                    await _userManager.AddToRoleAsync(user, model.NewRole);
                    var myRoles = await _userManager.GetRolesAsync(currentUser);
                    await _auditService.LogAsync(currentUser.Id, currentUser.Email!, myRoles.FirstOrDefault() ?? "", "UPDATE", $"Changed role of {user.Email} from {currentDbRole} to {model.NewRole}.", HttpContext.Connection.RemoteIpAddress?.ToString());
                }
                return RedirectToAction(nameof(Index));
            }

            model.IsOwnAccount = isOwnAccount;
            ViewBag.IsAlpha = User.IsInRole("SuperAdmin");
            ViewBag.IsEditingOwnAccount = isOwnAccount;
            ViewBag.Roles = User.IsInRole("SuperAdmin") && !isOwnAccount
                ? GetAllowedRolesForEditSelectList()
                : GetAllowedRolesSelectList();
            return View(model);
        }

        // GET: Users/ResetPassword/5
        public async Task<IActionResult> ResetPassword(string id)
        {
            if (id == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // SECURITY
            var roles = await _userManager.GetRolesAsync(user!);
            string userRole = roles.FirstOrDefault() ?? "Employee";
            if (!CanManageUser(currentUser, userRole, user.Id)) return Forbid();

            return View(new ResetPasswordViewModel { Id = user.Id, Email = user.Email });
        }

        // POST: Users/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var currentUser = await _userManager.GetUserAsync(User);
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            // SECURITY RE-CHECK
            var roles = await _userManager.GetRolesAsync(user!);
            string userRole = roles.FirstOrDefault() ?? "Employee";
            if (!CanManageUser(currentUser, userRole, user.Id)) return Forbid();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                var myRoles = await _userManager.GetRolesAsync(currentUser);
                await _auditService.LogAsync(currentUser.Id, currentUser.Email!, myRoles.FirstOrDefault() ?? "", "SECURITY", $"Reset password for {user.Email}.", HttpContext.Connection.RemoteIpAddress?.ToString(), isCritical: true);
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // ================= DELETE SYSTEM ADMIN (Alpha only) =================

        // POST: Users/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            // Only SuperAdmin can delete
            if (!User.IsInRole("SuperAdmin")) return Forbid();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Cannot delete self through this route
            if (user.Id == currentUser.Id) return Forbid();

            // Can only delete SystemAdmins
            var roles = await _userManager.GetRolesAsync(user!);
            string targetRole = roles.FirstOrDefault() ?? "Employee";
            if (targetRole != "SystemAdmin") return Forbid();

            var deletedEmail = user.Email;
            await _userManager.DeleteAsync(user!);
            await _auditService.LogAsync(currentUser.Id, currentUser.Email!, "SuperAdmin", "DELETE", $"Deleted user account: {deletedEmail} (role: {targetRole}).", HttpContext.Connection.RemoteIpAddress?.ToString(), isCritical: true);

            // Push notification to SuperAdmin
            await _notificationService.SendToRoleAsync("SuperAdmin",
                "🗑️ User Deleted",
                $"{deletedEmail} ({targetRole}) has been removed by {currentUser.Email}.");

            // In-App Notification
            await _inAppNotificationService.CreateForRoleAsync("SuperAdmin",
                "🗑️ User Deleted",
                $"{deletedEmail} has been removed.",
                "User", "bi-person-dash", "/Users/Index");
            return RedirectToAction(nameof(Index));
        }

        // ================= DELETE SELF (Alpha double-confirmation) =================

        // POST: Users/DeleteSelf
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelf(string password)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            // Must be SuperAdmin to use this
            if (!User.IsInRole("SuperAdmin")) return Forbid();

            // Verify password
            var passwordValid = await _userManager.CheckPasswordAsync(currentUser!, password);
            if (!passwordValid)
            {
                TempData["DeleteError"] = "Incorrect password. Account deletion cancelled.";
                return RedirectToAction(nameof(Edit), new { id = currentUser.Id });
            }

            // Delete and sign out
            var selfEmail = currentUser.Email;
            await _auditService.LogAsync(currentUser.Id, selfEmail!, "SuperAdmin", "SECURITY", $"Super Admin self-deleted account: {selfEmail}.", HttpContext.Connection.RemoteIpAddress?.ToString(), isCritical: true);
            await _userManager.DeleteAsync(currentUser);
            await _signInManager.SignOutAsync();
            return Redirect("/");
        }

        // ================= HELPER METHODS (Fixed Logic) =================

        // 1. THE SCOPE LOGIC (Restored Original Architecture)
        private bool CanManageUser(IdentityUser? me, string targetRole, string targetId)
        {
            // Null guard
            if (me == null) return false;

            // I can always see myself
            if (me.Id == targetId) return true;

            // SuperAdmin -> Can ONLY manage SystemAdmins
            if (User.IsInRole("SuperAdmin"))
            {
                return targetRole == "SystemAdmin";
            }

            // SystemAdmin -> Can manage Managers, Staff, Employees
            // SystemAdmin CANNOT manage SuperAdmin or other SystemAdmins
            if (User.IsInRole("SystemAdmin"))
            {
                return targetRole != "SuperAdmin" && targetRole != "SystemAdmin";
            }

            return false;
        }

        // 2. THE DATA SOURCE — for CREATE (Alpha creates Betas only)
        private List<string> GetAllowedRolesList()
        {
            var roles = new List<string>();

            if (User.IsInRole("SuperAdmin"))
            {
                // SuperAdmin can only create SystemAdmins
                roles.Add("SystemAdmin");
            }
            else if (User.IsInRole("SystemAdmin"))
            {
                // SystemAdmin can create everyone else
                roles.Add("DepartmentManager");
                roles.Add("WarehouseStaff");
                roles.Add("Employee");
            }

            return roles;
        }

        // 2b. THE DATA SOURCE — for EDIT (Alpha can also promote to SuperAdmin)
        private List<string> GetAllowedRolesForEditList()
        {
            var roles = GetAllowedRolesList();

            if (User.IsInRole("SuperAdmin"))
            {
                // SuperAdmin can promote a SystemAdmin to SuperAdmin
                roles.Add("SuperAdmin");
            }

            return roles;
        }

        // 3. THE UI HELPERS (Returns SelectList)
        private SelectList GetAllowedRolesSelectList()
        {
            return new SelectList(GetAllowedRolesList());
        }

        private SelectList GetAllowedRolesForEditSelectList()
        {
            return new SelectList(GetAllowedRolesForEditList());
        }

        // 4. THE VALIDATOR (Checks against Strings, not Objects)
        private bool IsRoleAllowed(string role)
        {
            return GetAllowedRolesList().Contains(role);
        }

        // ================= EMPLOYEE RECORDS (Read-Only for DepartmentManager) =================

        // GET: Users/EmployeeRecords
        [Authorize(Roles = "DepartmentManager")]
        public async Task<IActionResult> EmployeeRecords(string? searchString, int? page)
        {
            var allUsers = await _userManager.Users.ToListAsync();
            var records = new List<EmployeeRecordViewModel>();

            foreach (var user in allUsers)
            {
                if (user == null) continue;
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault() ?? "Employee";

                // DepartmentManager can only see staff-level roles (not admins)
                if (role == "WarehouseStaff" || role == "Employee" || role == "DepartmentManager")
                {
                    // Filter by search string if provided
                    if (!string.IsNullOrEmpty(searchString) && 
                        !(user.Email != null && user.Email.Contains(searchString, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var pendingLeaves = await _context.LeaveRequests.CountAsync(l => l.UserID == user.Id && l.Status == "Pending");
                    var approvedLeaves = await _context.LeaveRequests.CountAsync(l => l.UserID == user.Id && l.Status == "Approved");

                    records.Add(new EmployeeRecordViewModel
                    {
                        Id = user.Id,
                        Email = user.Email ?? "",
                        Role = role,
                        PendingLeaves = pendingLeaves,
                        ApprovedLeaves = approvedLeaves
                    });
                }
            }

            // Sort by Email
            var sortedRecords = records.OrderBy(r => r.Email).ToList();

            ViewBag.SearchString = searchString;
            ViewBag.TotalCount = sortedRecords.Count;

            return View(PaginatedList<EmployeeRecordViewModel>.Create(sortedRecords, page ?? 1, 10));
        }
    }
}