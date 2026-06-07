using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.GetTicketsOnTable;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class GetTicketsOnTableEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cashier/tables/{tableId:int}/tickets",
            async (int tableId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetTicketsOnTable.Query(tableId), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.CashierViewTicket)
            .WithTags("Tickets")
            .WithName("GetTicketsOnTable");
    }
}
