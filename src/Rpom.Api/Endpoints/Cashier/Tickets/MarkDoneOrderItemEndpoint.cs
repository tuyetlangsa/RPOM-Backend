using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.MarkDoneOrderItem;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class MarkDoneOrderItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/{ticketId:long}/order-items/mark-done",
                async (long ticketId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<MarkDoneOrderItem.Response> result =
                        await sender.Send(new MarkDoneOrderItem.Command(ticketId, request.OrderItemIds), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderItemMarkDone)
            .WithTags("Tickets")
            .WithName("MarkDoneOrderItem")
            .Produces<ApiResult<MarkDoneOrderItem.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Mark one or more order items DONE (READY → DONE). Auto-closes Order if all items terminal.")
            .WithDescription("Request: route ticketId + JSON body { orderItemIds: long[] }. Bumps KITCHEN + FLOOR_PLAN.");
    }

    internal sealed record Request(IReadOnlyList<long> OrderItemIds);
}
