namespace Rpom.Infrastructure.Payments;

public sealed class SePayOptions
{
    public const string SectionName = "SePay";

    /// <summary>Webhook shared secret. Required for the webhook to accept calls.</summary>
    //public string ApiKey { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public string BankCode { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    //Prefix for the payment reference code embedded in the memo
    public string ReferencePrefix { get; set; } = "RPOM";

    public string QrImageBaseUrl { get; set; } = "https://qr.sepay.vn/img";
}
