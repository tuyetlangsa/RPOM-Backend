using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.SendOrder;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class SendOrderEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/{ticketId:long}/send-order",
            async (long ticketId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new SendOrder.Command(ticketId), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.OrderSendKitchen)
            .WithTags("Tickets")
            .WithName("SendOrder")
            .Produces<ApiResult<SendOrder.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Send the ticket's cart to the kitchen.")
            .WithDescription("Request: route ticketId (long). Copies cart→order items, snapshots kitchen station, clears cart, recomputes ticket. Response: 200 OK — JSON body { orderId, orderNumber, itemCount, totalAmount }. 409 if cart empty.");
    }
}
