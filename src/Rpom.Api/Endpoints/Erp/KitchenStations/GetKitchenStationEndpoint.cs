using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.KitchenStations.GetKitchenStation;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.KitchenStations;

internal sealed class GetKitchenStationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/kitchen-stations/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetKitchenStation.Response> result = await sender.Send(new GetKitchenStation.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("KitchenStations")
            .WithName("GetKitchenStation")
            .Produces<ApiResult<GetKitchenStation.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a kitchen station by id.")
            .WithDescription("Request: route id (int). Response: 200 OK — JSON GetKitchenStation.Response.");
    }
}
