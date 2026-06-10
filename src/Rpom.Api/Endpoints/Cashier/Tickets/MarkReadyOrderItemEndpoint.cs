using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.MarkReadyOrderItem;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class MarkReadyOrderItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/{ticketId:long}/order-items/mark-ready",
                async (long ticketId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<MarkReadyOrderItem.Response> result =
                        await sender.Send(new MarkReadyOrderItem.Command(ticketId, request.OrderItemIds), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderItemMarkReady)
            .WithTags("Tickets")
            .WithName("MarkReadyOrderItem")
            .Produces<ApiResult<MarkReadyOrderItem.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Mark one or more order items READY (PROCESSING → READY).")
            .WithDescription("Request: route ticketId + JSON body { orderItemIds: long[] }. Bumps KITCHEN.");
    }

    internal sealed record Request(IReadOnlyList<long> OrderItemIds);
}
