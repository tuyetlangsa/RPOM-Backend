using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Kitchen.StartCookOrderItem;

namespace Rpom.Api.Endpoints.Kitchen;

internal sealed class StartCookOrderItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/kitchen/order-items/start-cook",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new StartCookOrderItem.Command(request.OrderItemIds ?? []), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderItemStartCooking)
            .Produces<ApiResult<StartCookOrderItem.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Kitchen")
            .WithName("StartCookOrderItem")
            .WithSummary("Bếp bắt đầu chế biến món (PENDING→PROCESSING) + trừ kho. Gate theo khu bếp của phiên.");
    }

    internal sealed record Request(IReadOnlyList<long>? OrderItemIds);
}
