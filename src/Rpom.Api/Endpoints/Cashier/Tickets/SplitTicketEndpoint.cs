using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.SplitTicket;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class SplitTicketEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/split",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var items = (request.Items ?? [])
                        .Select(i => new SplitTicket.SplitItemInput(i.OrderItemId, i.Quantity))
                        .ToList();
                    var result = await sender.Send(new SplitTicket.Command(
                        request.SourceTicketId,
                        request.DestinationTicketId,
                        request.DestinationTableId,
                        request.GuestCount,
                        items), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.TicketSplit)
            .Produces<ApiResult<SplitTicket.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Tickets")
            .WithName("SplitTicket")
            .WithSummary("Split items to existing or new invoice (same area)");
    }

    internal sealed record Request(
        long SourceTicketId,
        long? DestinationTicketId,
        int? DestinationTableId,
        short? GuestCount,
        IReadOnlyList<SplitItemRequest>? Items);

    internal sealed record SplitItemRequest(long OrderItemId, decimal Quantity);
}
