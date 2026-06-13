using System.Globalization;

namespace Rpom.Application.Configuration;

/// <summary>
///     Closed set of config value types. Drives FE widget choice (checkbox / text /
///     number / time) and update-time validation. BOOL and TIME are supported for
///     future configs even though current rows are only TEXT / NUMBER.
/// </summary>
public static class ConfigValueType
{
    public const string Bool = "BOOL";
    public const string Text = "TEXT";
    public const string Number = "NUMBER";
    public const string Time = "TIME";

    public static readonly IReadOnlyList<string> All = new[] { Bool, Text, Number, Time };

    /// <summary>
    ///     True if <paramref name="value" /> is valid for <paramref name="valueType" />.
    ///     Null/blank is always valid (means "unset" — caller falls back). TEXT accepts
    ///     anything. NUMBER parses as invariant decimal, BOOL as bool, TIME as HH:mm.
    /// </summary>
    public static bool IsValidValue(string valueType, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return valueType switch
        {
            // Float (no AllowThousands): "1,5" must be rejected — comma is not the
            // invariant decimal separator, only "." is.
            Number => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            Bool => bool.TryParse(value, out _),
            Time => TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out _),
            _ => true // TEXT (and any unknown type) imposes no format constraint
        };
    }
}
