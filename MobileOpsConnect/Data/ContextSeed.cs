using Microsoft.AspNetCore.Identity;

namespace MobileOpsConnect.Data
{
    public static class ContextSeed
    {
        public static async Task SeedRolesAsync(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // 1. Define ALL Roles
            string[] roleNames = {
                "SuperAdmin",         // Alpha
                "SystemAdmin",        // Beta
                "DepartmentManager",  // Charlie
                "WarehouseStaff",     // Delta
                "Employee"            // Echo
            };

            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Define The 5 Users
            var users = new List<(string Email, string Role)>
            {
                ("alpha@mobileops.com", "SuperAdmin"),
                ("beta@mobileops.com", "SystemAdmin"),
                ("charlie@mobileops.com", "DepartmentManager"),
                ("delta@mobileops.com", "WarehouseStaff"),
                ("echo@mobileops.com", "Employee")
            };

            // 3. Create Users & Assign Roles
            foreach (var (email, role) in users)
            {
                var userCheck = await userManager.FindByEmailAsync(email);
                if (userCheck == null)
                {
                    var newUser = new IdentityUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        PhoneNumberConfirmed = true
                    };

                    // Default Password: "Password123!"
                    var result = await userManager.CreateAsync(newUser, "Password123!");

                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(newUser, role);
                    }
                }
            }
        }
    }
}