using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Access.SetStaffPermissions;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Access;

internal sealed class SetStaffPermissionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/access/staff-accounts/{id:int}/permissions",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<SetStaffPermissions.Response> result = await sender.Send(
                        new SetStaffPermissions.Command(id, request.PermissionCodes ?? []), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.PermissionAssign)
            .WithTags("Access")
            .WithName("SetStaffPermissions")
            .Produces<ApiResult<SetStaffPermissions.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Full-replace the permission grants for an account.");
    }

    internal sealed record Request(IReadOnlyList<string>? PermissionCodes);
}
