using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.SendOrder;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class SendOrderEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/{ticketId:long}/send-order",
                async (long ticketId, [FromBody] Request? request, ISender sender, CancellationToken ct) =>
                {
                    Result<SendOrder.Response> result =
                        await sender.Send(new SendOrder.Command(ticketId, request?.CartItemIds), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderSendKitchen)
            .WithTags("Tickets")
            .WithName("SendOrder")
            .Produces<ApiResult<SendOrder.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Send the ticket's cart (or a subset) to the kitchen.")
            .WithDescription("""
    Request: route ticketId (long); optional JSON body { cartItemIds?:long[] } — omit/empty = send the
    whole cart, a strict subset = partial send (kept lines move to a new draft batch). Copies cart→order
    items, snapshots kitchen station, recomputes ticket. Response: 200 OK — JSON body { orderId,
    orderNumber, itemCount, totalAmount }. 409 if cart empty.
""");
    }

    internal sealed record Request(IReadOnlyList<long>? CartItemIds);
}
