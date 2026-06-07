using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.AcquireTableLock;

namespace Rpom.Api.Endpoints.Cashier.Tables;

internal sealed class AcquireTableLockEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tables/{tableId:int}/lock",
            async (int tableId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new AcquireTableLock.Command(tableId), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.TicketOpen)
            .WithTags("Tables")
            .WithName("AcquireTableLock")
            .Produces<ApiResult<AcquireTableLock.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Acquire or heartbeat the operation lock on a table.")
            .WithDescription("Request: route tableId (int). Idempotent for the holder; refreshes the heartbeat. Response: 200 OK — JSON AcquireTableLock.Response. 409 if another staff holds a live lock.");
    }
}
