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
                    var lines = request.Lines
                        .Select(l => new CancelOrderItem.CancelLine(l.OrderItemId, l.Quantity))
                        .ToList();
                    Result<CancelOrderItem.Response> result =
                        await sender.Send(new CancelOrderItem.Command(ticketId, lines), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderItemCancelPending)
            .WithTags("Tickets")
            .WithName("CancelOrderItem")
            .Produces<ApiResult<CancelOrderItem.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Cancel one or more pending order items (PENDING → CANCELLED).")
            .WithDescription(
                "Request: route ticketId + JSON body { lines: [{ orderItemId, quantity? }, ...] }. "
                + "Only from PENDING. Omit quantity (or set to 0) to cancel the entire line; "
                + "a quantity less than the line's qty performs a partial cancel (reduces the "
                + "original line and creates a CANCELLED shadow row for the cancelled portion). "
                + "Auto-closes Order if all items terminal.");
    }

    internal sealed record LineRequest(long OrderItemId, decimal? Quantity = null);

    internal sealed record Request(IReadOnlyList<LineRequest> Lines);
}
