using System.Security.Claims;

namespace Rpom.Infrastructure.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static int GetStaffAccountId(this ClaimsPrincipal? principal)
    {
        var sub = principal?.FindFirst(CustomClaims.Sub)?.Value;
        return int.TryParse(sub, out var id)
            ? id
            : throw new ApplicationException("Staff identifier is unavailable");
    }

    public static string GetUsername(this ClaimsPrincipal? principal)
    {
        return principal?.FindFirst(CustomClaims.Username)?.Value
            ?? throw new ApplicationException("Username claim is missing");
    }

    public static int? GetCounterId(this ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(CustomClaims.CounterId)?.Value;
        return int.TryParse(value, out var id) ? id : null;
    }

    public static int? GetKitchenStationId(this ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(CustomClaims.KitchenStationId)?.Value;
        return int.TryParse(value, out var id) ? id : null;
    }

    public static HashSet<string> GetPermissions(this ClaimsPrincipal? principal)
    {
        var permissionClaims = principal?.FindAll(CustomClaims.Permission)
            ?? throw new ApplicationException("Permissions are unavailable");

        return permissionClaims.Select(c => c.Value).ToHashSet();
    }
}
