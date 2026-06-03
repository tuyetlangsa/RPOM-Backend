using Rpom.Domain.Common;

namespace Rpom.Application.Abstraction.Authorization;

/// <summary>
/// Resolves the effective permission set for a staff account. Called once per
/// authenticated request by CustomClaimsTransformation; not cached at this layer.
/// </summary>
public interface IPermissionService
{
    Task<Result<PermissionsResponse>> GetUserPermissionsAsync(int staffAccountId, CancellationToken cancellationToken = default);
}
