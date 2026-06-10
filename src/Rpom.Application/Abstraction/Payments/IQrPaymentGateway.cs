namespace Rpom.Application.Abstraction.Payments;
public interface IQrPaymentGateway
{
    bool IsConfigured { get; }

    string BuildReferenceCode(long paymentDetailId);

    long? TryParsePaymentDetailId(string? code, string? content);

    QrCodeDescriptor BuildQrCode(string referenceCode, decimal amount);

    /// <summary>
    /// Validates the HMAC-SHA256 signature of the SePay webhook. SePay computes 
    /// the signature as <c>HMAC-SHA256(secret, "{timestamp}.{raw_body}")</c> and includes it 
    /// via headers <c>X-SePay-Signature: sha256={hex}</c> and <c>X-SePay-Timestamp: {unix_seconds}</c>.
    /// Requires the original unparsed RAW body. Includes timestamp-based replay attack protection.
    /// </summary>
    bool VerifyWebhookSignature(string? rawBody, string? signatureHeader, string? timestampHeader);
}

public sealed record QrCodeDescriptor(
    string ReferenceCode,
    string QrImageUrl,
    string AccountNumber,
    string BankCode,
    string AccountName,
    decimal Amount);
