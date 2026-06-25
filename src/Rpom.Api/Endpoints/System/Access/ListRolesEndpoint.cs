using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Access.ListRoles;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Access;

internal sealed class ListRolesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/access/roles", async (ISender sender, CancellationToken ct) =>
            {
                Result<ListRoles.Response> result = await sender.Send(new ListRoles.Query(), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.StaffAccountManage)
            .WithTags("Access")
            .WithName("ListRoles")
            .Produces<ApiResult<ListRoles.Response>>()
            .WithSummary("List active roles with account counts (left tree + role selector).");
    }
}
