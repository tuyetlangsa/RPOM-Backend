using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.TransferTable;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class TransferTableEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/{ticketId:long}/transfer-table",
                async (long ticketId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<TransferTable.Response> result =
                        await sender.Send(new TransferTable.Command(ticketId, request.TargetTableId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.TicketTransfer)
            .WithTags("Tickets")
            .WithName("TransferTable")
            .Produces<ApiResult<TransferTable.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Transfer an OPEN ticket to another table at the same counter.")
            .WithDescription(
                "Request: route ticketId + JSON body { targetTableId }. Same counter only. SENT items keep "
                + "prices; on area change the DRAFT cart is cleared and service charge is re-snapshotted per "
                + "the transfer.use_target_area_service_charge config.");
    }

    internal sealed record Request(int TargetTableId);
}
