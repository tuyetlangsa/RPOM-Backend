using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Payments.CreateCashPayment;

namespace Rpom.Api.Endpoints.Cashier.Payments;

internal sealed class CreateCashPaymentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/tickets/{ticketId:long}/payments/cash",
            async (long ticketId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(
                    new CreateCashPayment.Command(ticketId, request.Amount, request.ReceivedAmount, request.Notes), ct);
                return result.MatchCreated(r => $"/api/tickets/{ticketId}/payments/{r.PaymentId}");
            })
            .RequireAuthorization(Permissions.PaymentCash)
            .Produces<ApiResult<CreateCashPayment.Response>>(StatusCodes.Status201Created)
            .WithTags("Payments")
            .WithName("CreateCashPayment")
            .WithSummary("Record a cash payment (settled immediately).");
    }

    internal sealed record Request(decimal Amount, decimal ReceivedAmount, string? Notes);
}
