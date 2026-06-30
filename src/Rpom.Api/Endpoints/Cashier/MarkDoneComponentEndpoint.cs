using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.MarkDoneComponent;

namespace Rpom.Api.Endpoints.Cashier;

internal sealed class MarkDoneComponentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/order-item-components/mark-done",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new MarkDoneComponent.Command(request.TicketId, request.OrderItemDetailIds ?? []), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderItemMarkDone)
            .Produces<ApiResult<MarkDoneComponent.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("OrderItems")
            .WithName("MarkDoneComponent")
            .WithSummary("Cashier/Order staff have completed the set menu components (READY→DONE). Table lock is required.");
    }

    internal sealed record Request(long TicketId, IReadOnlyList<int>? OrderItemDetailIds);
}
