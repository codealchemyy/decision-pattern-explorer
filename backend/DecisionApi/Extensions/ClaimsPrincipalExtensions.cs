using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DecisionApi.Extensions;

//GET /decisions (mine)
//Add a helper to read the JWT sub as Guid
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (Guid.TryParse(sub, out var id)) return id;

        throw new InvalidOperationException("JWT 'sub' claim is missing or not a Guid.");
    }
}
