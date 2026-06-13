using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
            $"&des={Uri.EscapeDataString(referenceCode)}" +
            $"&showinfo=true" +
            $"&store=NHA%20HANG%20RPOM";

        return new QrCodeDescriptor(
            referenceCode,
            url,
            _options.AccountNumber,
            _options.BankCode,
            _options.AccountName,
            amount);
    }

    public bool VerifyWebhookSignature(string? rawBody, string? signatureHeader, string? timestampHeader)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret)) return false;
        if (string.IsNullOrWhiteSpace(rawBody)) return false;
        if (string.IsNullOrWhiteSpace(signatureHeader)) return false;
        if (string.IsNullOrWhiteSpace(timestampHeader)) return false;

        // Chống replay: timestamp (unix seconds) phải nằm trong ±5 phút so với hiện tại.
        if (!long.TryParse(timestampHeader.Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var ts))
            return false;
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(nowUnix - ts) > 300) return false;

        // SePay ký HMAC-SHA256 trên chuỗi "{timestamp}.{raw_body}" với Secret Key,
        // gửi kèm header X-SePay-Signature: sha256={hex}. Phải dùng RAW body gốc.
        var signingString = $"{timestampHeader.Trim()}.{rawBody}";
        var keyBytes = Encoding.UTF8.GetBytes(_options.WebhookSecret);
        var hashBytes = HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(signingString));
        var expected = "sha256=" + Convert.ToHexString(hashBytes).ToLowerInvariant();

        // So sánh timing-safe.
        return CryptographicEquals(signatureHeader.Trim(), expected);
    }

    private static bool CryptographicEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
