using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Payments.HandleSePayWebhook;

namespace Rpom.Api.Endpoints.System.Webhooks;
internal sealed class SePayWebhookEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/webhooks/sepay",
            async (
                [FromBody] Request request,
                //[FromHeader(Name = "Authorization")] string? authorization,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new HandleSePayWebhook.Command(
                    //AuthorizationHeader: authorization,
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
                    RawPayload: JsonSerializer.Serialize(request));

                var result = await sender.Send(command, ct);
                return result.MatchOk();
            })
            .AllowAnonymous()
            .WithTags("Payments")
            .WithName("SePayWebhook")
            .WithSummary("SePay bank-transaction webhook — settles matching QR payments.");
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
