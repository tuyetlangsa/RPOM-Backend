using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.RemoveDiscount;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class RemoveDiscountEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/cashier/tickets/{ticketId:long}/discount",
            async (long ticketId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new RemoveDiscount.Command(ticketId), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.OrderSendKitchen)
            .WithTags("Tickets")
            .WithName("RemoveDiscount")
            .Produces<ApiResult<RemoveDiscount.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Remove the current discount from the ticket.")
            .WithDescription("""
    Request: route ticketId (long). Clears all discount percentages and amounts, then recomputes.
    Idempotent — OK if no discount applied. Response: 200 OK — JSON body { ticketId, totalAmount }.
""");
    }
}
