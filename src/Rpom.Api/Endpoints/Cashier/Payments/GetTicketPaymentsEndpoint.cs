using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Payments.GetTicketPayments;

namespace Rpom.Api.Endpoints.Cashier.Payments;

internal sealed class GetTicketPaymentsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/tickets/{ticketId:long}/payments",
            async (long ticketId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetTicketPayments.Query(ticketId), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.TicketViewDetail)
            .WithTags("Payments")
            .WithName("GetTicketPayments")
            .WithSummary("Get the list of payments and totals of a ticket for cashier operations (cancellation/deletion/reconciliation).");
    }
}
