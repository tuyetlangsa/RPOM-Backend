using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.SplitTicketPreview;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class SplitTicketPreviewEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/split/preview",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var items = (request.Items ?? [])
                        .Select(i => new SplitTicketPreview.SplitItemInput(i.OrderItemId, i.Quantity))
                        .ToList();
                    var result = await sender.Send(new SplitTicketPreview.Query(
                        request.SourceTicketId,
                        request.DestinationTicketId,
                        request.DestinationTableId,
                        request.GuestCount,
                        items), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.TicketSplit)
            .Produces<ApiResult<SplitTicketPreview.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Tickets")
            .WithName("SplitTicketPreview")
            .WithSummary("Dry-run split: trả tổng tiền 2 phiếu sau khi tách, không ghi DB");
    }

    internal sealed record Request(
        long SourceTicketId,
        long? DestinationTicketId,
        int? DestinationTableId,
        short? GuestCount,
        IReadOnlyList<SplitItemRequest>? Items);

    internal sealed record SplitItemRequest(long OrderItemId, decimal Quantity);
}
