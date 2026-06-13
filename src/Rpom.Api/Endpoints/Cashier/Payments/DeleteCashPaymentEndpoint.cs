using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Payments.DeleteCashPayment;

namespace Rpom.Api.Endpoints.Cashier.Payments;

internal sealed class DeleteCashPaymentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/payments/{paymentId:long}",
            async (long paymentId, [FromQuery] string? reason, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new DeleteCashPayment.Command(paymentId, reason), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.PaymentDeleteRecord)
            .WithTags("Payments")
            .WithName("DeleteCashPayment")
            .WithSummary("Soft delete a SUCCESS cash payment transaction (including the accompanying change amount if applicable).");
    }
}
