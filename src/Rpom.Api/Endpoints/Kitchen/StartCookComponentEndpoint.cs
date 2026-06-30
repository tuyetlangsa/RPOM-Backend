using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Kitchen.StartCookComponent;

namespace Rpom.Api.Endpoints.Kitchen;

internal sealed class StartCookComponentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/kitchen/order-item-components/start-cook",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new StartCookComponent.Command(request.OrderItemDetailIds ?? []), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderItemStartCooking)
            .Produces<ApiResult<StartCookComponent.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Kitchen")
            .WithName("StartCookComponent")
            .WithSummary("The kitchen starts preparing the set menu components (PENDING → PROCESSING) + subtracts inventory. Gate by kitchen area.");
    }

    internal sealed record Request(IReadOnlyList<int>? OrderItemDetailIds);
}
