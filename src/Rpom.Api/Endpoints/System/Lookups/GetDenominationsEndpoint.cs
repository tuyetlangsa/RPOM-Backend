using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Lookups.GetDenominations;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Lookups;

internal sealed class GetDenominationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/lookups/denominations", async (ISender sender, CancellationToken ct) =>
            {
                Result<IReadOnlyList<GetDenominations.DenominationItem>> result =
                    await sender.Send(new GetDenominations.Query(), ct);
                return result.MatchOk();
            })
            .RequireAuthorization()
            .WithTags("Lookups")
            .WithName("GetDenominations")
            .Produces<ApiResult<IReadOnlyList<GetDenominations.DenominationItem>>>()
            .WithSummary("List cash denominations for selection lists.")
            .WithDescription("Request: none. Response: 200 OK — JSON array of GetDenominations.Response.");
    }
}
