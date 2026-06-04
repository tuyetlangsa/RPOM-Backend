using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

/// <summary>
/// 1 "price column" in a parent PriceTable — conditional pricing scenario
/// with its own (Time × Day × Area) applicability and its own PriceEntry set.
/// Full spec: docs/RPOM_Pricing_Spec.md.
/// </summary>
public class PriceVariant : Entity
{
    public int Id { get; set; }

    /// <summary>Parent strategy. Cascade delete: variants cannot exist standalone.</summary>
    public int PriceTableId { get; set; }

    /// <summary>V_DEFAULT, V_HAPPY_HOUR, V_VIP, V_TET. Unique within PriceTable.</summary>
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>Inclusive. NULL = whole day. E.g. 14:00 for happy hour.</summary>
    public TimeOnly? BeginTime { get; set; }

    /// <summary>Exclusive. NULL = whole day. E.g. 17:00.</summary>
    public TimeOnly? EndTime { get; set; }

    /// <summary>
    /// Day-of-week bitmask: Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64.
    /// Mon-Fri = 31, weekend = 96. NULL = all days.
    /// Check match: <c>(DayMask &amp; dayBit) != 0</c>.
    /// </summary>
    public int? DayMask { get; set; }

    /// <summary>true = applies to all Areas (junction ignored). false = only Areas in PriceVariantArea.</summary>
    public bool AppliesToAllAreas { get; set; } = true;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual PriceTable PriceTable { get; set; } = null!;
    public virtual ICollection<PriceEntry> PriceEntries { get; set; } = new List<PriceEntry>();
    public virtual ICollection<PriceVariantArea> PriceVariantAreas { get; set; } = new List<PriceVariantArea>();

    /// <summary>
    /// Specificity: number of dimensions specified (non-NULL / non-AllAreas), range 0..3.
    /// Used by most-specific-wins resolution and save-time conflict validator.
    /// </summary>
    public int Specificity =>
        (BeginTime is not null || EndTime is not null ? 1 : 0)
      + (DayMask is not null ? 1 : 0)
      + (AppliesToAllAreas ? 0 : 1);
}
