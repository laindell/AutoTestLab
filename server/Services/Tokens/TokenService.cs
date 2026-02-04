using server.Models;
using server.Services.Tokens;

namespace Server.Services;

public class TokenService : ITokenService
{
    public string GenerateAccessToken(User user)
    {
        // TODO: тимчасова заглушка
        return "ACCESS_TOKEN";
    }

    public string GenerateRefreshToken()
    {
        return Guid.NewGuid().ToString();
    }
}
