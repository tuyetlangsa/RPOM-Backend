using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Tickets.GetTicketAuditLog;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class GetTicketAuditLogEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cashier/tickets/{ticketId:long}/audit-log",
                async (long ticketId, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new GetTicketAuditLog.Query(ticketId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.TicketAuditLog)
            .WithTags("Tickets")
            .WithName("GetTicketAuditLog")
            .Produces<ApiResult<IReadOnlyList<GetTicketAuditLog.Response>>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get audit/history log for a ticket");
    }
}
