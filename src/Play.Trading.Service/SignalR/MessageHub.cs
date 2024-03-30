using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Play.Trading.Service.StateMachines;

namespace Play.Trading.Service.SignalR;

/// <summary>
/// The purpose of this class to send messages from the server to the client. 
/// This class acts an intermediate of communication between client and server.
/// </summary>
[Authorize]
public class MessageHub : Hub
{
    public async Task SendStatusAsync(PurchaseState status)
    {
        if (Clients is not null)
        {
            // Send message to the user that authenticated to our service.
            await Clients.User(Context.UserIdentifier).SendAsync("ReceivePurchaseStatus", status);
        }
    }
}