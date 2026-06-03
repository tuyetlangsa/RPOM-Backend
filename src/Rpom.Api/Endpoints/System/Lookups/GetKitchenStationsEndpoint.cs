using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Lookups.GetKitchenStations;

namespace Rpom.Api.Endpoints.System.Lookups;

internal sealed class GetKitchenStationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/lookups/kitchen-stations", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetKitchenStations.Query(), ct);
            return result.MatchOk();
        })
        .RequireAuthorization()
        .WithTags("Lookups")
        .WithName("GetKitchenStations");
    }
}
