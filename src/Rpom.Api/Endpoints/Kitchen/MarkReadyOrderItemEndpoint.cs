using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Kitchen.MarkReadyOrderItem;

namespace Rpom.Api.Endpoints.Kitchen;

internal sealed class MarkReadyOrderItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/kitchen/order-items/mark-ready",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new MarkReadyOrderItem.Command(request.OrderItemIds ?? []), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderItemMarkReady)
            .Produces<ApiResult<MarkReadyOrderItem.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Kitchen")
            .WithName("MarkReadyOrderItem")
            .WithSummary("Mark one or more order items READY (PROCESSING → READY).");
    }

    internal sealed record Request(IReadOnlyList<long>? OrderItemIds);
}
