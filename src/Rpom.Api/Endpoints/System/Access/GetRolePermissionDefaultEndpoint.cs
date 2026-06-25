using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Access.GetRolePermissionDefault;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Access;

internal sealed class GetRolePermissionDefaultEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/access/role-permission-defaults/{roleCode}",
                async (string roleCode, ISender sender, CancellationToken ct) =>
                {
                    Result<GetRolePermissionDefault.Response> result =
                        await sender.Send(new GetRolePermissionDefault.Query(roleCode), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.PermissionAssign)
            .WithTags("Access")
            .WithName("GetRolePermissionDefault")
            .Produces<ApiResult<GetRolePermissionDefault.Response>>()
            .WithSummary("Default permission codes for a role (pre-fill the permission grid).");
    }
}
