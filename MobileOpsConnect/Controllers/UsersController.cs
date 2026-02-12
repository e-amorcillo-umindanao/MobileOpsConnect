using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileOpsConnect.Models;
using System.Data;

namespace MobileOpsConnect.Controllers
{
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

        // GET: Users (The List)
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var allUsers = await _userManager.Users.ToListAsync();
            var userList = new List<UserViewModel>();

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault() ?? "No Role";

                // === SCOPE FILTER: Who can I see? ===
                if (CanManageUser(currentUser, role, user.Id))
                {
                    userList.Add(new UserViewModel
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Role = role,
                        // If you added IsActive to ApplicationUser, you can access it here:
                        // IsActive = ((ApplicationUser)user).IsActive 
                    });
                }
            }
            return View(userList);
        }

        // GET: Create
        public IActionResult Create() => View();

        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string email, string password, string role)
        {
            // SECURITY: Beta cannot create Alpha
            if (User.IsInRole("SystemAdmin") && role == "SuperAdmin") return Forbid();

            var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);
                return RedirectToAction(nameof(Index));
            }
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View();
        }

        // GET: Edit (Change Role)
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // SECURITY CHECK
            var currentUser = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(user);
            if (!CanManageUser(currentUser, userRoles.FirstOrDefault(), user.Id)) return Forbid();

            var model = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email,
                CurrentRole = userRoles.FirstOrDefault()
            };
            return View(model);
        }

        // POST: Edit (Save Role)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            // 1. Remove old roles
            var oldRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, oldRoles);

            // 2. Add new role
            await _userManager.AddToRoleAsync(user, model.NewRole);

            return RedirectToAction(nameof(Index));
        }

        // GET: Reset Password
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            return View(new ResetPasswordViewModel { Id = id, Email = user.Email });
        }

        // POST: Reset Password
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            // Force reset using a token (bypassing old password)
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded) return RedirectToAction(nameof(Index));

            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View(model);
        }

        // === HELPER: The Logic for Alpha vs Beta ===
        private bool CanManageUser(IdentityUser admin, string targetRole, string targetId)
        {
            // I can always manage myself
            if (admin.Id == targetId) return true;

            // Alpha (SuperAdmin) manages Beta (SystemAdmin)
            if (User.IsInRole("SuperAdmin"))
            {
                return targetRole == "SystemAdmin";
            }

            // Beta (SystemAdmin) manages everyone ELSE (Managers, Staff, Employees)
            if (User.IsInRole("SystemAdmin"))
            {
                return targetRole != "SuperAdmin" && targetRole != "SystemAdmin";
            }

            return false;
        }
    }

    // --- VIEW MODELS ---
    public class UserViewModel { public string Id { get; set; } public string Email { get; set; } public string Role { get; set; } }
    public class EditUserViewModel { public string Id { get; set; } public string Email { get; set; } public string CurrentRole { get; set; } public string NewRole { get; set; } }
    public class ResetPasswordViewModel { public string Id { get; set; } public string Email { get; set; } public string NewPassword { get; set; } }
}