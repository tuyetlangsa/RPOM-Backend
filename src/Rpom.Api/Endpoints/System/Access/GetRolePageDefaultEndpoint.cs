using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Access.GetRolePageDefault;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Access;

internal sealed class GetRolePageDefaultEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/access/role-page-defaults/{roleCode}",
                async (string roleCode, ISender sender, CancellationToken ct) =>
                {
                    Result<GetRolePageDefault.Response> result =
                        await sender.Send(new GetRolePageDefault.Query(roleCode), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.PageAccessAssign)
            .WithTags("Access")
            .WithName("GetRolePageDefault")
            .Produces<ApiResult<GetRolePageDefault.Response>>()
            .WithSummary("Return the default page-access template for a role.")
            .WithDescription(
                "Request: route roleCode (string). Response: 200 OK — JSON GetRolePageDefault.Response "
                + "(default page codes; empty for roles without a template). FE uses this to pre-fill the "
                + "page-access grid before submitting via PUT page-access.");
    }
}
