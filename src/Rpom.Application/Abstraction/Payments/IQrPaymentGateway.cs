namespace Rpom.Application.Abstraction.Payments;
public interface IQrPaymentGateway
{
    bool IsConfigured { get; }

    string BuildReferenceCode(long paymentDetailId);

    long? TryParsePaymentDetailId(string? code, string? content);

    QrCodeDescriptor BuildQrCode(string referenceCode, decimal amount);

    /// <summary>
    /// Validate the inbound webhook authorization header (SePay sends
    /// <c>Authorization: Apikey &lt;key&gt;</c>). Returns true when authentic.
    /// </summary>
    bool VerifyWebhookApiKey(string? authorizationHeader);
}

public sealed record QrCodeDescriptor(
    string ReferenceCode,
    string QrImageUrl,
    string AccountNumber,
    string BankCode,
    string AccountName,
    decimal Amount);
