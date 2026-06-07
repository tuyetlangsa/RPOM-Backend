using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.GetTicketDetails;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class GetTicketDetailsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cashier/tickets/{ticketId:long}",
                async (long ticketId, ISender sender, CancellationToken ct) =>
                {
                    Result<GetTicketDetails.Response> result =
                        await sender.Send(new GetTicketDetails.Query(ticketId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.CashierViewTicket)
            .WithTags("Tickets")
            .WithName("GetTicketDetails")
            .Produces<ApiResult<GetTicketDetails.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get full ticket detail (5 sections).")
            .WithDescription(
                "Request: route ticketId (long). Response: 200 OK — JSON GetTicketDetails.Response (Info, ItemDetails, OrderBatches, OrderingItems, Payment).");
    }
}
