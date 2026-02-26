using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace MobileOpsConnect.Hubs
{
    public class InventoryHub : Hub
    {
        private readonly UserManager<IdentityUser> _userManager;

        public InventoryHub(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        // On connect, add the user to a SignalR group named after their role
        // so controllers can target broadcasts to specific roles.
        public override async Task OnConnectedAsync()
        {
            var user = await _userManager.GetUserAsync(Context.User!);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                foreach (var role in roles)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"role_{role}");
                }
            }
            await base.OnConnectedAsync();
        }

        // Client methods are invoked from the server via IHubContext<InventoryHub>
        // Clients listen for:
        //   "StockUpdated"       → (int productId, string productName, int newQuantity, string action)
        //   "LeaveStatusChanged" → (int leaveId, string status, string employeeEmail)
    }
}
