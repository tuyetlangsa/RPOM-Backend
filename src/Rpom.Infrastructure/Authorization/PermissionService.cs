using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Authorization;
using Rpom.Application.Abstraction.Data;
using Rpom.Domain.Common;

namespace Rpom.Infrastructure.Authorization;

/// <summary>
/// Resolves a staff account's effective permission codes by joining
/// StaffAccountPermission → Permission. Called once per authenticated request
/// by CustomClaimsTransformation. Result is augmented into ClaimsPrincipal so
/// downstream endpoint checks read from claims (in-memory) rather than re-query.
/// </summary>
internal sealed class PermissionService(IDbContext dbContext) : IPermissionService
{
    public async Task<Result<PermissionsResponse>> GetUserPermissionsAsync(
        int staffAccountId,
        CancellationToken cancellationToken = default)
    {
        var codes = await dbContext.StaffAccountPermissions
            .Where(x => x.StaffAccountId == staffAccountId)
            .Select(x => x.Permission.Code)
            .ToListAsync(cancellationToken);

        return Result.Success(new PermissionsResponse(staffAccountId, codes.ToHashSet()));
    }
}
