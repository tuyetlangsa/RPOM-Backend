using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.RemoveCartItem;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class RemoveCartItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/cashier/tickets/{ticketId:long}/cart-items/{cartItemId:long}",
                async (long ticketId, long cartItemId, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(new RemoveCartItem.Command(ticketId, cartItemId), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.OrderAddItems)
            .WithTags("Tickets")
            .WithName("RemoveCartItem")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Remove a cart line from the ticket.")
            .WithDescription(
                "Request: route ticketId (long) + cartItemId (long). Detail rows cascade. Response: 204 No Content.");
    }
}
