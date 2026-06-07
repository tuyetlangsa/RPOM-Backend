using System.Security.Claims;

namespace Rpom.Infrastructure.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static int GetStaffAccountId(this ClaimsPrincipal? principal)
    {
        string? sub = principal?.FindFirst(CustomClaims.Sub)?.Value;
        return int.TryParse(sub, out int id)
            ? id
            : throw new ApplicationException("Staff identifier is unavailable");
    }

    public static string GetUsername(this ClaimsPrincipal? principal)
    {
        return principal?.FindFirst(CustomClaims.Username)?.Value
               ?? throw new ApplicationException("Username claim is missing");
    }

    public static HashSet<string> GetPermissions(this ClaimsPrincipal? principal)
    {
        IEnumerable<Claim> permissionClaims = principal?.FindAll(CustomClaims.Permission)
                                              ?? throw new ApplicationException("Permissions are unavailable");

        return permissionClaims.Select(c => c.Value).ToHashSet();
    }
}
