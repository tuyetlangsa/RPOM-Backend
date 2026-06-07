using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Pricing;

namespace Rpom.Infrastructure.Pricing;

/// <summary>
///     Loads all rounding digits into IMemoryCache (5-min TTL). One cache entry
///     holds the full key→digits map; invalidated by Invalidate() from the
///     UpdateRoundingConfig handler. Falls back to RoundingKeys.Defaults when a
///     key is missing (unseeded). Synchronous GetDigits blocks on first load —
///     acceptable, the map is tiny (14 rows).
/// </summary>
internal sealed class RoundingConfigService(IDbContext dbContext, IMemoryCache cache)
    : IRoundingConfig, IRoundingCacheInvalidator
{
    private const string CacheKey = "rounding_config_map";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public void Invalidate() => cache.Remove(CacheKey);

    public int GetDigits(string keyCode)
    {
        Dictionary<string, int>? map = cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return dbContext.RoundingConfigs
                .AsNoTracking()
                .ToDictionary(x => x.KeyCode, x => (int)x.Digits);
        })!;

        if (map.TryGetValue(keyCode, out int digits))
        {
            return digits;
        }

        return RoundingKeys.Defaults.TryGetValue(keyCode, out short def) ? def : 0;
    }
}
