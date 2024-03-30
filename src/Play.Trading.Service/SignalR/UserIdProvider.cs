using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Play.Trading.Service.SignalR;

/// <summary>
/// The class provides the way of telling SignalR how to identity the user that is logged in.
/// In our case, we identity a user that is authenticated via the "sub" claim. Notice this class is automatically
/// mapped to the SignalR context.
/// </summary>
public class UserIdProvider : IUserIdProvider
{
    public string GetUserId(HubConnectionContext connection)
    {
        return connection.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
    }
}
