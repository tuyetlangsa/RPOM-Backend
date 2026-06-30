using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Kitchen.PinKitchenOrder;

namespace Rpom.Api.Endpoints.Kitchen;

internal sealed class PinKitchenOrderEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/kitchen/orders/{orderId:long}/pin",
                async (long orderId, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new PinKitchenOrder.Command(orderId), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.KdsView)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Kitchen")
            .WithName("PinKitchenOrder")
            .WithSummary("Pin one order batch to the top of the current kitchen's KDS screen (the next pin will float on top of the previous one).");
    }
}
