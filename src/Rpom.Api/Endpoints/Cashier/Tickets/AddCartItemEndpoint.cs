using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.AddCartItem;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class AddCartItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/{ticketId:long}/cart-items",
                async (long ticketId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var details = (request.Details ?? Array.Empty<DetailRequest>())
                        .Select(d => new AddCartItem.DetailInput(
                            d.ChoiceCategoryId, d.ItemId, d.ComponentType, d.Quantity, d.Notes))
                        .ToList();
                    Result<AddCartItem.Response> result = await sender.Send(new AddCartItem.Command(
                        ticketId, request.ItemId, request.Quantity, request.Notes, details), ct);
                    return result.MatchCreated(r => $"/api/cashier/tickets/{ticketId}/cart-items/{r.CartItemId}");
                })
            .RequireAuthorization(Permissions.OrderAddItems)
            .WithTags("Tickets")
            .WithName("AddCartItem")
            .Produces<ApiResult<AddCartItem.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Add an item (single or set menu) to the ticket cart.")
            .WithDescription("""
    Request: route ticketId (long); JSON body { itemId:int, quantity:decimal, notes?:string, details?:[{
    choiceCategoryId?:int, itemId:int, componentType:'MAIN_COMPONENT'|'MODIFIER', quantity:decimal,
    notes?:string }] }. Set-menu selections validated server-side; price computed server-side. Response:
    201 Created — JSON body { cartItemId, orderId, lineTotal }.
""");
    }

    internal sealed record Request(
        int ItemId,
        decimal Quantity,
        string? Notes,
        IReadOnlyList<DetailRequest>? Details);

    internal sealed record DetailRequest(
        int? ChoiceCategoryId,
        int ItemId,
        string ComponentType,
        decimal Quantity,
        string? Notes);
}
