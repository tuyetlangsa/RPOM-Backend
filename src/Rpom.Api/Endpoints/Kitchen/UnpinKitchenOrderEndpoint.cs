using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Kitchen.UnpinKitchenOrder;

namespace Rpom.Api.Endpoints.Kitchen;

internal sealed class UnpinKitchenOrderEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/kitchen/orders/{orderId:long}/unpin",
                async (long orderId, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new UnpinKitchenOrder.Command(orderId), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.KdsView)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Kitchen")
            .WithName("UnpinKitchenOrder")
            .WithSummary("Unpin a batch of orders from the current kitchen's KDS screen.");
    }
}
