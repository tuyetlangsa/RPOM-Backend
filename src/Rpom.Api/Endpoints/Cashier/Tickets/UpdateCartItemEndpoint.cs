using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.UpdateCartItem;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class UpdateCartItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/cashier/tickets/{ticketId:long}/cart-items/{cartItemId:long}",
            async (long ticketId, long cartItemId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new UpdateCartItem.Command(
                    ticketId, cartItemId, request.Quantity, request.Notes), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.OrderAddItems)
            .WithTags("Tickets")
            .WithName("UpdateCartItem")
            .Produces<ApiResult<UpdateCartItem.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update a cart line's quantity and notes.")
            .WithDescription("Request: route ticketId (long) + cartItemId (long); JSON body { quantity:decimal, notes?:string }. Reconfiguring modifiers = remove + add. Response: 200 OK — JSON body { cartItemId, lineTotal }.");
    }

    internal sealed record Request(decimal Quantity, string? Notes);
}
