using Rpom.Domain.Common;

namespace Rpom.Domain.Configuration;

/// <summary>
/// Per-field rounding precision. KeyCode = PK (e.g. "I_ROUNDLINESUBTOTAL").
/// Digits = decimal places (0 = round to whole VND, 2 = keep 2 dp).
/// Owner edits a row → recompute on the NEXT mutation uses the new precision.
/// Seeded idempotently by RoundingConfigSeeder. See pricing spec §1.
/// </summary>
public class RoundingConfig : Entity
{
    /// <summary>Code key, e.g. "I_ROUNDLINESUBTOTAL".</summary>
    public string KeyCode { get; set; } = null!;

    /// <summary>Decimal places. 0 = whole VND, 2 = 2 dp.</summary>
    public short Digits { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
