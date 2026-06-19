using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Kitchen.ProcessReturnItem;

namespace Rpom.Api.Endpoints.Kitchen;

internal sealed class ProcessReturnItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/kitchen/order-items/{orderItemId:long}/process-return",
                async (long orderItemId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new ProcessReturnItem.Command(orderItemId, request.Restock, request.Note), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderItemProcessReturn)
            .Produces<ApiResult<ProcessReturnItem.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Kitchen")
            .WithName("ProcessReturnItem")
            .WithSummary("Kitchen processing for return items + option to return materials/items to inventory.");
    }

    internal sealed record Request(bool Restock, string? Note);
}
