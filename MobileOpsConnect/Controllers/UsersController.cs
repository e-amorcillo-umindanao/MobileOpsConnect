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
        private readonly SignInManager<IdentityUser> _signInManager;

        public UsersController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
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
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            bool isOwnAccount = (user.Id == currentUser.Id);

            // 1. RE-CHECK PERMISSIONS (Security Fix)
            var roles = await _userManager.GetRolesAsync(user);
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

        // ================= DELETE SYSTEM ADMIN (Alpha only) =================

        // POST: Users/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            // Only SuperAdmin can delete
            if (!User.IsInRole("SuperAdmin")) return Forbid();

            var currentUser = await _userManager.GetUserAsync(User);
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Cannot delete self through this route
            if (user.Id == currentUser.Id) return Forbid();

            // Can only delete SystemAdmins
            var roles = await _userManager.GetRolesAsync(user);
            string targetRole = roles.FirstOrDefault() ?? "Employee";
            if (targetRole != "SystemAdmin") return Forbid();

            await _userManager.DeleteAsync(user);
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
            var passwordValid = await _userManager.CheckPasswordAsync(currentUser, password);
            if (!passwordValid)
            {
                TempData["DeleteError"] = "Incorrect password. Account deletion cancelled.";
                return RedirectToAction(nameof(Edit), new { id = currentUser.Id });
            }

            // Delete and sign out
            await _userManager.DeleteAsync(currentUser);
            await _signInManager.SignOutAsync();
            return Redirect("/");
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

        // 2. THE DATA SOURCE — for CREATE (Alpha creates Betas only)
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

        // 2b. THE DATA SOURCE — for EDIT (Alpha can also promote to SuperAdmin)
        private List<string> GetAllowedRolesForEditList()
        {
            var roles = GetAllowedRolesList();

            if (User.IsInRole("SuperAdmin"))
            {
                // Alpha can promote a Beta to SuperAdmin
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
    }
}