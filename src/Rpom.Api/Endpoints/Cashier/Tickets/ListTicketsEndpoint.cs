using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Tickets.ListTickets;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class ListTicketsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cashier/tickets",
                async (string? status, DateTime? fromDate, DateTime? toDate, int? counterId,
                    int? areaId, int? shiftId, string? search, int pageNumber, int pageSize,
                    ISender sender, CancellationToken ct) =>
                {
                    var query = new ListTickets.Query(
                        status, fromDate, toDate, counterId, areaId, shiftId,
                        search, pageNumber < 1 ? 1 : pageNumber, pageSize < 1 ? 50 : pageSize);
                    var result = await sender.Send(query, ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.TicketList)
            .WithTags("Tickets")
            .WithName("ListTickets")
            .Produces<ApiResult<Domain.Common.Page<ListTickets.Response>>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("List all tickets with filters and pagination");
    }
}
