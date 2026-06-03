namespace Rpom.Application.Abstraction.Configuration;

/// <summary>
/// Flat key-value config reader/writer. Reads each call → DB (no cache for v1).
/// Typed accessors are extension methods on top of <see cref="GetAsync"/>.
/// </summary>
public interface IConfigValueService
{
    /// <summary>Returns raw string value, or NULL if code not seeded / unset.</summary>
    Task<string?> GetAsync(string code, CancellationToken ct = default);

    /// <summary>Returns NULL if no config row exists for that code.</summary>
    Task<bool> ExistsAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Update existing config row's value. Throws or returns false if code doesn't exist —
    /// new configs must be added via ConfigCodes catalog + seeder + migration cycle.
    /// </summary>
    Task<bool> SetAsync(string code, string? value, int? updatedByStaffAccountId, CancellationToken ct = default);
}

/// <summary>
/// Typed accessor extensions. Each call parses the raw string with fallback.
/// </summary>
public static class ConfigValueServiceExtensions
{
    public static async Task<int> GetIntAsync(
        this IConfigValueService svc, string code, int fallback, CancellationToken ct = default)
    {
        var raw = await svc.GetAsync(code, ct);
        return int.TryParse(raw, out var v) ? v : fallback;
    }

    public static async Task<decimal> GetDecimalAsync(
        this IConfigValueService svc, string code, decimal fallback, CancellationToken ct = default)
    {
        var raw = await svc.GetAsync(code, ct);
        return decimal.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    public static async Task<bool> GetBoolAsync(
        this IConfigValueService svc, string code, bool fallback, CancellationToken ct = default)
    {
        var raw = await svc.GetAsync(code, ct);
        return bool.TryParse(raw, out var v) ? v : fallback;
    }

    public static async Task<string> GetStringAsync(
        this IConfigValueService svc, string code, string fallback, CancellationToken ct = default)
    {
        return await svc.GetAsync(code, ct) ?? fallback;
    }

    public static async Task<TimeOnly> GetTimeAsync(
        this IConfigValueService svc, string code, TimeOnly fallback, CancellationToken ct = default)
    {
        var raw = await svc.GetAsync(code, ct);
        return TimeOnly.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }
}
