using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Rpom.Application.Abstraction.Payments;

namespace Rpom.Infrastructure.Payments;

/// <summary>
/// SePay implementation of <see cref="IQrPaymentGateway"/>. Stateless: builds
/// VietQR image URLs via SePay's renderer, derives/parses the per-payment
/// reference code, and validates the webhook Apikey header. No outbound HTTP is
/// needed to create a QR (the image URL is rendered by SePay on demand); the
/// money confirmation arrives asynchronously via the webhook.
/// </summary>
internal sealed class SePayQrPaymentGateway : IQrPaymentGateway
{
    private readonly SePayOptions _options;
    private readonly Regex _referenceRegex;

    public SePayQrPaymentGateway(IOptions<SePayOptions> options)
    {
        _options = options.Value;
        var prefix = string.IsNullOrWhiteSpace(_options.ReferencePrefix) ? "RPOM" : _options.ReferencePrefix;
        _referenceRegex = new Regex(
            $"{Regex.Escape(prefix)}(?<id>\\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.AccountNumber)
        && !string.IsNullOrWhiteSpace(_options.BankCode);

    public string BuildReferenceCode(long paymentDetailId)
        => $"{_options.ReferencePrefix}{paymentDetailId}";

    public long? TryParsePaymentDetailId(string? code, string? content)
    {
        // SePay may pre-extract the code; prefer it, then fall back to the memo.
        foreach (var source in new[] { code, content })
        {
            if (string.IsNullOrWhiteSpace(source)) continue;
            var match = _referenceRegex.Match(source);
            if (match.Success
                && long.TryParse(match.Groups["id"].Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var id))
            {
                return id;
            }
        }
        return null;
    }

    public QrCodeDescriptor BuildQrCode(string referenceCode, decimal amount)
    {
        var amountText = ((decimal)amount).ToString(CultureInfo.InvariantCulture);
        var url =
            $"{_options.QrImageBaseUrl}?bank={Uri.EscapeDataString(_options.BankCode)}" +
            $"&acc={Uri.EscapeDataString(_options.AccountNumber)}" +
            $"&template=compact" +
            $"&amount={Uri.EscapeDataString(amountText)}" +
            $"&des={Uri.EscapeDataString(referenceCode)}";

        return new QrCodeDescriptor(
            referenceCode,
            url,
            _options.AccountNumber,
            _options.BankCode,
            _options.AccountName,
            amount);
    }

    //public bool VerifyWebhookApiKey(string? authorizationHeader)
    //{
    //    if (string.IsNullOrWhiteSpace(_options.ApiKey)) return false;
    //    if (string.IsNullOrWhiteSpace(authorizationHeader)) return false;

    //    // SePay sends "Authorization: Apikey <key>".
    //    var value = authorizationHeader.Trim();
    //    const string scheme = "Apikey ";
    //    if (value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
    //        value = value[scheme.Length..].Trim();

    //    return CryptographicEquals(value, _options.ApiKey);
    //}

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
