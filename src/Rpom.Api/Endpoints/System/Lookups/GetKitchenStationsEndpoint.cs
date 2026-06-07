using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Lookups.GetKitchenStations;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Lookups;

internal sealed class GetKitchenStationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/lookups/kitchen-stations", async (ISender sender, CancellationToken ct) =>
            {
                Result<IReadOnlyList<GetKitchenStations.KitchenStationItem>> result =
                    await sender.Send(new GetKitchenStations.Query(), ct);
                return result.MatchOk();
            })
            .RequireAuthorization()
            .WithTags("Lookups")
            .WithName("GetKitchenStations")
            .Produces<ApiResult<IReadOnlyList<GetKitchenStations.KitchenStationItem>>>()
            .WithSummary("List kitchen stations for selection lists.")
            .WithDescription("Request: none. Response: 200 OK — JSON array of GetKitchenStations.Response.");
    }
}
