namespace Rpom.Application.Abstraction.Pricing;

/// <summary>
/// Reads rounding digit counts. Implementation caches all 14 keys
/// (IMemoryCache, 5-min TTL) and invalidates on UpdateRoundingConfig.
/// </summary>
public interface IRoundingConfig
{
    /// <summary>Digit count for a key. Falls back to RoundingKeys.Defaults if unseeded.</summary>
    int GetDigits(string keyCode);
}
