using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.StartCookOrderItem;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class StartCookOrderItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/{ticketId:long}/order-items/start-cook",
                async (long ticketId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<StartCookOrderItem.Response> result =
                        await sender.Send(new StartCookOrderItem.Command(ticketId, request.OrderItemIds), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderItemStartCooking)
            .WithTags("Tickets")
            .WithName("StartCookOrderItem")
            .Produces<ApiResult<StartCookOrderItem.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Start cooking one or more order items (PENDING → PROCESSING).")
            .WithDescription("Request: route ticketId + JSON body { orderItemIds: long[] }. Bumps KITCHEN.");
    }

    internal sealed record Request(IReadOnlyList<long> OrderItemIds);
}
