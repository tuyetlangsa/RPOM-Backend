using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.UpdateCartItem;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class UpdateCartItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/cashier/tickets/{ticketId:long}/cart-items/{cartItemId:long}",
                async (long ticketId, long cartItemId, [FromBody] Request request, ISender sender,
                    CancellationToken ct) =>
                {
                    Result<UpdateCartItem.Response> result = await sender.Send(new UpdateCartItem.Command(
                        ticketId, cartItemId, request.Quantity, request.Notes, request.Details), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderAddItems)
            .WithTags("Tickets")
            .WithName("UpdateCartItem")
            .Produces<ApiResult<UpdateCartItem.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update a cart line: qty, notes, or reconfigure set-menu modifiers.")
            .WithDescription("""
    Request: route ticketId (long) + cartItemId (long); JSON body { quantity:decimal, notes?:string,
    details?:[{ choiceCategoryId?:int, itemId:int, componentType:string, quantity:decimal, notes?:string
    }] }. For set menus: FE sends the complete desired modifier set; BE diffs against existing — matched
    updates, qty=0 deletes, new inserts, then validates. Main qty=0 removes the cart line. Response: 200
    OK — JSON body { cartItemId, lineTotal }.
""");
    }

    internal sealed record Request(decimal Quantity, string? Notes, IReadOnlyList<UpdateCartItem.DetailInput>? Details);
}
