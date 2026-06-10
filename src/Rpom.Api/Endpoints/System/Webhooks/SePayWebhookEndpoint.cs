using System.Text;
using System.Text.Json;
using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Payments.HandleSePayWebhook;

namespace Rpom.Api.Endpoints.System.Webhooks;

/// <summary>
/// SePay payment webhook (the QR "callback"). Anonymous at the framework level —
/// authenticated inside the handler via HMAC-SHA256 over the RAW request body
/// (headers X-SePay-Signature + X-SePay-Timestamp). We read the raw body stream
/// directly (NOT [FromBody]) so the bytes used to recompute the signature match
/// exactly what SePay signed. Idempotent: re-deliveries are no-ops.
/// </summary>
internal sealed class SePayWebhookEndpoint : IEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/webhooks/sepay",
            async (HttpContext httpContext, ISender sender, CancellationToken ct) =>
            {
                // Original RAW body — used to reconstruct the HMAC signature.
                string rawBody;
                using (var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8))
                {
                    rawBody = await reader.ReadToEndAsync(ct);
                }

                var signature = httpContext.Request.Headers["X-SePay-Signature"].ToString();
                var timestamp = httpContext.Request.Headers["X-SePay-Timestamp"].ToString();

                Request? request;
                try
                {
                    request = JsonSerializer.Deserialize<Request>(rawBody, JsonOptions);
                }
                catch (JsonException)
                {
                    request = null;
                }
                if (request is null)
                    return Microsoft.AspNetCore.Http.Results.BadRequest();

                var command = new HandleSePayWebhook.Command(
                    Signature: signature,
                    Timestamp: timestamp,
                    Id: request.Id,
                    Gateway: request.Gateway,
                    TransactionDate: request.TransactionDate,
                    AccountNumber: request.AccountNumber,
                    SubAccount: request.SubAccount,
                    Code: request.Code,
                    Content: request.Content,
                    TransferType: request.TransferType,
                    TransferAmount: request.TransferAmount,
                    Accumulated: request.Accumulated,
                    ReferenceCode: request.ReferenceCode,
                    Description: request.Description,
                    RawPayload: rawBody);

                var result = await sender.Send(command, ct);
                return result.MatchOk();
            })
            .AllowAnonymous()
            .WithTags("Payments")
            .WithName("SePayWebhook")
            .WithSummary("SePay bank-transaction webhook — settles matching QR payments (HMAC-SHA256 verified).");
    }

    //Mirror of the SePay webhook payload (camelCase JSON)
    internal sealed record Request(
        long Id,
        string? Gateway,
        string? TransactionDate,
        string? AccountNumber,
        string? SubAccount,
        string? Code,
        string? Content,
        string? TransferType,
        decimal TransferAmount,
        decimal Accumulated,
        string? ReferenceCode,
        string? Description);
}
