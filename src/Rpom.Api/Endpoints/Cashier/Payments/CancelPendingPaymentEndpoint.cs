using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Payments.CancelPendingPayment;

namespace Rpom.Api.Endpoints.Cashier.Payments;

internal sealed class CancelPendingPaymentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payments/{paymentId:long}/cancel",
            async (long paymentId, [FromBody] Request? request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(
                    new CancelPendingPayment.Command(paymentId, request?.Reason), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.PaymentCancelPending)
            .WithTags("Payments")
            .WithName("CancelPendingPayment")
            .WithSummary("Cancel a PENDING payment (e.g. abandoned QR scan) and release its hold.");
    }

    internal sealed record Request(string? Reason);
}
