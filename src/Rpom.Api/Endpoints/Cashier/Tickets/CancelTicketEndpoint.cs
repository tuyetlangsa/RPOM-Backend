using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.CancelTicket;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class CancelTicketEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/{ticketId:long}/cancel",
                async (long ticketId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CancelTicket.Response> result = await sender.Send(
                        new CancelTicket.Command(
                            ticketId, request.ManagerStaffId, request.CancellationReasonId, request.CancellationNote),
                        ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.TicketCancel)
            .WithTags("Tickets")
            .WithName("CancelTicket")
            .Produces<ApiResult<CancelTicket.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Cancel an OPEN ticket (manager-authorized).")
            .WithDescription(
                "Request: route ticketId + JSON body { managerStaffId, cancellationReasonId, cancellationNote? }. "
                + "Allowed only when the bill is empty (every order item already cancelled) and there is no pending "
                + "or successful payment. Drops the unsent draft cart, releases the table lock, and moves the ticket "
                + "to the hard-terminal CANCELLED state. No refund is performed.");
    }

    internal sealed record Request(int ManagerStaffId, int CancellationReasonId, string? CancellationNote);
}
