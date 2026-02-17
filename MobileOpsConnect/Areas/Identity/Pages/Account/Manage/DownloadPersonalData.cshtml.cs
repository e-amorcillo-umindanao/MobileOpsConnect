using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MobileOpsConnect.Areas.Identity.Pages.Account.Manage
{
    public class DownloadPersonalDataModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;

        public DownloadPersonalDataModel(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Collect personal data into a dictionary
            var personalData = new Dictionary<string, string>();

            // Add Identity user properties
            var personalDataProps = typeof(IdentityUser).GetProperties()
                .Where(prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));

            foreach (var p in personalDataProps)
            {
                personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
            }

            // Add common fields manually
            personalData["Id"] = user.Id;
            personalData["UserName"] = user.UserName ?? "";
            personalData["Email"] = user.Email ?? "";
            personalData["PhoneNumber"] = user.PhoneNumber ?? "";
            personalData["EmailConfirmed"] = user.EmailConfirmed.ToString();
            personalData["TwoFactorEnabled"] = user.TwoFactorEnabled.ToString();

            Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
            return new FileContentResult(
                JsonSerializer.SerializeToUtf8Bytes(personalData, new JsonSerializerOptions { WriteIndented = true }),
                "application/json");
        }
    }
}
