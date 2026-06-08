using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Payments.CreateQrPayment;

namespace Rpom.Api.Endpoints.Cashier.Payments;

internal sealed class CreateQrPaymentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/tickets/{ticketId:long}/payments/qr",
            async (long ticketId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(
                    new CreateQrPayment.Command(ticketId, request.Amount, request.Notes), ct);
                return result.MatchCreated(r => $"/api/tickets/{ticketId}/payments/{r.PaymentId}");
            })
            .RequireAuthorization(Permissions.PaymentQr)
            .WithTags("Payments")
            .WithName("CreateQrPayment")
            .WithSummary("Create a PENDING QR payment and return the VietQR (SePay) for the customer to scan.");
    }

    internal sealed record Request(decimal Amount, string? Notes);
}
