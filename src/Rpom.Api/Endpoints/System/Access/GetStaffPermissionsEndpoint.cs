using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Access.GetStaffPermissions;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Access;

internal sealed class GetStaffPermissionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/access/staff-accounts/{id:int}/permissions",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetStaffPermissions.Response> result =
                        await sender.Send(new GetStaffPermissions.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.PermissionAssign)
            .WithTags("Access")
            .WithName("GetStaffPermissions")
            .Produces<ApiResult<GetStaffPermissions.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Full permission catalog with granted flags for an account.");
    }
}
