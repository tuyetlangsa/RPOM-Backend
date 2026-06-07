using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.GetTicketDetails;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class GetTicketDetailsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cashier/tickets/{ticketId:long}",
            async (long ticketId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetTicketDetails.Query(ticketId), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.CashierViewTicket)
            .WithTags("Tickets")
            .WithName("GetTicketDetails");
    }
}
