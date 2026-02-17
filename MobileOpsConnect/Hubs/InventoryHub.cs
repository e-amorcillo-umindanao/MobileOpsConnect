using Microsoft.AspNetCore.SignalR;

namespace MobileOpsConnect.Hubs
{
    public class InventoryHub : Hub
    {
        // Client methods are invoked from the server via IHubContext<InventoryHub>
        // Clients listen for:
        //   "StockUpdated"       → (int productId, string productName, int newQuantity, string action)
        //   "LeaveStatusChanged" → (int leaveId, string status, string employeeEmail)
    }
}
