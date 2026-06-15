using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.CloseTicket;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class CloseTicketEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/{ticketId:long}/close",
                async (long ticketId, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new CloseTicket.Command(ticketId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.TicketClose)
            .Produces<ApiResult<CloseTicket.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Tickets")
            .WithName("CloseTicket")
            .WithSummary("Close OPEN ticket");
    }
}
