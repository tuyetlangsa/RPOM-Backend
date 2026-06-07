using Rpom.Domain.Common;

namespace Rpom.Domain.Configuration;

/// <summary>
///     Flat key-value store for runtime configuration. Code = PK string (e.g.
///     <c>"reservation.pre_buffer_minutes"</c>); Value always stored as text and
///     parsed by caller. Defaults seeded by ConfigValueSeeder on startup
///     (idempotent — only inserts missing rows).
/// </summary>
public class ConfigValue : Entity
{
    /// <summary>Dot-namespaced code (e.g. "reservation.pre_buffer_minutes").</summary>
    public string Code { get; set; } = null!;

    /// <summary>Stored as text. NULL = unset (caller uses fallback).</summary>
    public string? Value { get; set; }

    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Soft ref to last editor; NULL when seeder set the value.</summary>
    public int? UpdatedByStaffAccountId { get; set; }
}
