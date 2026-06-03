using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Rpom.Application.Abstraction.Authorization;
using Rpom.Application.Abstraction.Exceptions;
using Rpom.Domain.Common;
using Rpom.Infrastructure.Authentication;

namespace Rpom.Infrastructure.Authorization;

/// <summary>
/// Runs after JWT validation per request. Pulls the StaffAccountId from
/// the <c>sub</c> claim, fetches the user's permissions from DB via
/// <see cref="IPermissionService"/>, and augments the ClaimsPrincipal
/// with one <c>permission</c> claim per code. Endpoints then check via
/// <c>.RequireAuthorization("&lt;permission_code&gt;")</c>.
/// </summary>
internal sealed class CustomClaimsTransformation(IServiceScopeFactory serviceScopeFactory)
    : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Already enriched (avoid re-running on each Authorize call).
        if (principal.HasClaim(c => c.Type == CustomClaims.Permission))
        {
            return principal;
        }

        if (!principal.HasClaim(c => c.Type == CustomClaims.Sub))
        {
            return principal;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();

        var staffAccountId = principal.GetStaffAccountId();

        Result<PermissionsResponse> result = await permissionService.GetUserPermissionsAsync(staffAccountId);

        if (result.IsFailure)
        {
            throw new RpomException(nameof(IPermissionService.GetUserPermissionsAsync), result.Error);
        }

        var claimsIdentity = new ClaimsIdentity();

        foreach (var permission in result.Value.Permissions)
        {
            claimsIdentity.AddClaim(new Claim(CustomClaims.Permission, permission));
        }

        principal.AddIdentity(claimsIdentity);
        return principal;
    }
}
