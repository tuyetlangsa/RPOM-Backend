using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Payments.GetPaymentStatus;

namespace Rpom.Api.Endpoints.Cashier.Payments;

internal sealed class GetPaymentStatusEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payments/{paymentId:long}/status",
            async (long paymentId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetPaymentStatus.Query(paymentId), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.TicketViewDetail)
            .Produces<ApiResult<GetPaymentStatus.Response>>(StatusCodes.Status200OK)
            .WithTags("Payments")
            .WithName("GetPaymentStatus")
            .WithSummary("Get payment status (Frontend polling for QR payment awaiting webhook confirmation)");
    }
}
