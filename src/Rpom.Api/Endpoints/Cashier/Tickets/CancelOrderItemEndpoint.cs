using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.CancelOrderItem;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class CancelOrderItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/{ticketId:long}/order-items/cancel",
                async (long ticketId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CancelOrderItem.Response> result =
                        await sender.Send(new CancelOrderItem.Command(ticketId, request.OrderItemIds), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderItemCancelPending)
            .WithTags("Tickets")
            .WithName("CancelOrderItem")
            .Produces<ApiResult<CancelOrderItem.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Cancel one or more pending order items (PENDING → CANCELLED).")
            .WithDescription("Request: route ticketId + JSON body { orderItemIds: long[] }. Only from PENDING. Auto-closes Order if all terminal.");
    }

    internal sealed record Request(IReadOnlyList<long> OrderItemIds);
}
