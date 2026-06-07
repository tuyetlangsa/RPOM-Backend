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
            .WithName("GetTicketsOnTable")
            .Produces<ApiResult<GetTicketsOnTable.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("List open tickets on a table.")
            .WithDescription("Request: route tableId (int). Response: 200 OK — JSON array of GetTicketsOnTable.Response.");
    }
}
