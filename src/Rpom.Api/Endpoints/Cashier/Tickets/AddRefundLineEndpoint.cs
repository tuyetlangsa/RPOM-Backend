using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.AddRefundLine;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class AddRefundLineEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/{ticketId:long}/order-items/{orderItemId:long}/refund-line",
                async (long ticketId, long orderItemId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<AddRefundLine.Response> result = await sender.Send(
                        new AddRefundLine.Command(
                            ticketId, orderItemId, request.Quantity, request.CancellationReasonId, request.CancellationNote),
                        ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.OrderItemRefundLine)
            .WithTags("Tickets")
            .WithName("AddRefundLine")
            .Produces<ApiResult<AddRefundLine.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Add a refund (return) line for a cooked order item.")
            .WithDescription(
                "Request: route ticketId + orderItemId + JSON body { quantity, cancellationReasonId, cancellationNote? }. "
                + "quantity is a positive magnitude; the server stores a negative DRAFT cart line linked to the original. "
                + "Then send it via the normal send-order endpoint. Original must be PROCESSING/READY/DONE.");
    }

    internal sealed record Request(decimal Quantity, int CancellationReasonId, string? CancellationNote);
}
