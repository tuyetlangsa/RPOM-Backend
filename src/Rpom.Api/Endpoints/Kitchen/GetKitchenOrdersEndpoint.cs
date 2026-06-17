using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Kitchen.GetKitchenOrders;

namespace Rpom.Api.Endpoints.Kitchen;

internal sealed class GetKitchenOrdersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/kitchen/orders",
                async (int kitchenStationId, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new GetKitchenOrders.Query(kitchenStationId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.KdsView)
            .Produces<ApiResult<GetKitchenOrders.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Kitchen")
            .WithName("GetKitchenOrders")
            .WithSummary("KDS: Batches sent to the kitchen + items belonging to the current kitchen station, sorted by sent time (earliest to latest).");
    }
}
