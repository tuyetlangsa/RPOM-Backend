using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Kitchen.MarkReadyComponent;

namespace Rpom.Api.Endpoints.Kitchen;

internal sealed class MarkReadyComponentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/kitchen/order-item-components/mark-ready",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new MarkReadyComponent.Command(request.OrderItemDetailIds ?? []), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderItemMarkReady)
            .Produces<ApiResult<MarkReadyComponent.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Kitchen")
            .WithName("MarkReadyComponent")
            .WithSummary("The kitchen prepares the set menu component (PROCESSING→READY). Gate according to kitchen area.");
    }

    internal sealed record Request(IReadOnlyList<int>? OrderItemDetailIds);
}
