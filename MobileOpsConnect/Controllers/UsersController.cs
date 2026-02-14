using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Models;

namespace MobileOpsConnect.Controllers
{
    // REVERTED: Only SuperAdmin and SystemAdmin can access this. 
    // Charlie (Manager) handles Leaves, not User Accounts.
    [Authorize(Roles = "SuperAdmin,SystemAdmin")]
    public class UsersController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var allUsers = await _userManager.Users.ToListAsync();
            var userViewModels = new List<UserViewModel>();

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault() ?? "Employee";

                // SCOPE LOGIC: Alpha sees Beta; Beta sees Staff.
                if (CanManageUser(currentUser, role, user.Id))
                {
                    userViewModels.Add(new UserViewModel
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Role = role
                    });
                }
            }

            return View(userViewModels);
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
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            string currentRole = roles.FirstOrDefault() ?? "Employee";

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
                NewRole = currentRole
            };

            ViewBag.Roles = GetAllowedRolesSelectList();
            return View(model);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, EditUserViewModel model)
        {
            if (id != model.Id) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // 1. RE-CHECK PERMISSIONS (Security Fix)
            var roles = await _userManager.GetRolesAsync(user);
            string currentDbRole = roles.FirstOrDefault() ?? "Employee";

            if (!CanManageUser(currentUser, currentDbRole, user.Id)) return Forbid();

            // 2. VALIDATE NEW ROLE (Privilege Escalation Fix)
            if (!IsRoleAllowed(model.NewRole)) return Forbid();

            // 3. PREVENT SELF-LOCKOUT (Logic Fix)
            if (user.Id == currentUser.Id && model.NewRole != currentDbRole)
            {
                ModelState.AddModelError("", "You cannot change your own role.");
                ViewBag.Roles = GetAllowedRolesSelectList();
                return View(model);
            }

            if (ModelState.IsValid)
            {
                // Logic: Only update role if it changed
                if (model.NewRole != currentDbRole)
                {
                    await _userManager.RemoveFromRoleAsync(user, currentDbRole);
                    await _userManager.AddToRoleAsync(user, model.NewRole);
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Roles = GetAllowedRolesSelectList();
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
            var roles = await _userManager.GetRolesAsync(user);
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
            var roles = await _userManager.GetRolesAsync(user);
            string userRole = roles.FirstOrDefault() ?? "Employee";
            if (!CanManageUser(currentUser, userRole, user.Id)) return Forbid();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // ================= HELPER METHODS (Fixed Logic) =================

        // 1. THE SCOPE LOGIC (Restored Original Architecture)
        private bool CanManageUser(IdentityUser me, string targetRole, string targetId)
        {
            // I can always see myself
            if (me.Id == targetId) return true;

            // Alpha (SuperAdmin) -> Can ONLY manage SystemAdmins (Beta)
            if (User.IsInRole("SuperAdmin"))
            {
                return targetRole == "SystemAdmin";
            }

            // Beta (SystemAdmin) -> Can manage Managers, Staff, Employees
            // Beta CANNOT manage SuperAdmin or other SystemAdmins
            if (User.IsInRole("SystemAdmin"))
            {
                return targetRole != "SuperAdmin" && targetRole != "SystemAdmin";
            }

            return false;
        }

        // 2. THE DATA SOURCE (Fixes the Crash)
        private List<string> GetAllowedRolesList()
        {
            var roles = new List<string>();

            if (User.IsInRole("SuperAdmin"))
            {
                // Alpha can only create Betas
                roles.Add("SystemAdmin");
            }
            else if (User.IsInRole("SystemAdmin"))
            {
                // Beta can create everyone else
                roles.Add("DepartmentManager");
                roles.Add("WarehouseStaff");
                roles.Add("Employee");
            }

            return roles;
        }

        // 3. THE UI HELPER (Returns SelectList)
        private SelectList GetAllowedRolesSelectList()
        {
            return new SelectList(GetAllowedRolesList());
        }

        // 4. THE VALIDATOR (Checks against Strings, not Objects)
        private bool IsRoleAllowed(string role)
        {
            return GetAllowedRolesList().Contains(role);
        }
    }
}