using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.ReleaseTableLock;

namespace Rpom.Api.Endpoints.Cashier.Tables;

internal sealed class ReleaseTableLockEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/cashier/tables/{tableId:int}/lock",
            async (int tableId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ReleaseTableLock.Command(tableId), ct);
                return result.MatchNoContent();
            })
            .RequireAuthorization(Permissions.TicketOpen)
            .WithTags("Tables")
            .WithName("ReleaseTableLock")
            .Produces(StatusCodes.Status204NoContent)
            .WithSummary("Release my operation lock on a table.")
            .WithDescription("Request: route tableId (int). No-op if absent or held by another staff. Response: 204 No Content.");
    }
}
