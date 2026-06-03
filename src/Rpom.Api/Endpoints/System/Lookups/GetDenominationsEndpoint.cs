using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Lookups.GetDenominations;

namespace Rpom.Api.Endpoints.System.Lookups;

internal sealed class GetDenominationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/lookups/denominations", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDenominations.Query(), ct);
            return result.MatchOk();
        })
        .RequireAuthorization()
        .WithTags("Lookups")
        .WithName("GetDenominations");
    }
}
