using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access.GetMyMenu;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Access;

internal sealed class GetMyMenuEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/access/my-menu", async (ISender sender, CancellationToken ct) =>
            {
                Result<GetMyMenu.Response> result = await sender.Send(new GetMyMenu.Query(), ct);
                return result.MatchOk();
            })
            .RequireAuthorization()
            .WithTags("Access")
            .WithName("GetMyMenu")
            .Produces<ApiResult<GetMyMenu.Response>>()
            .WithSummary("Return the module/page navigation tree the current account can access.")
            .WithDescription(
                "Request: none (Bearer token). Response: 200 OK — JSON GetMyMenu.Response (modules → pages). "
                + "Source for FE sidebar + route guard.");
    }
}
