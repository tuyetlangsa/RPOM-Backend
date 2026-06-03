using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

/// <summary>
/// A "price column" within a parent PriceTable — one priced scenario with its
/// own applicability conditions and its own set of PriceEntry rows.
/// See PriceVariant resolution algorithm in RPOM_Logical_ERD.md §7.6.
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

    /// <summary>Higher wins when multiple variants match the order context (tie-breaker).</summary>
    public short Priority { get; set; }

    /// <summary>Override parent PriceTable.BeginDate. NULL = inherit parent.</summary>
    public DateOnly? BeginDate { get; set; }

    /// <summary>Override parent PriceTable.EndDate. NULL = inherit parent.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Daily time window start (e.g. 14:00 for happy hour). NULL = whole day.</summary>
    public TimeOnly? BeginTime { get; set; }
    public TimeOnly? EndTime { get; set; }

    /// <summary>
    /// Comma-separated day-of-week: 1=Mon..7=Sun (e.g. "1,2,3,4,5" weekdays).
    /// NULL = all days.
    /// </summary>
    public string? DaysOfWeek { get; set; }

    /// <summary>true = applies to all Areas; false = only Areas in PriceVariantArea junction.</summary>
    public bool AppliesToAllAreas { get; set; } = true;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual PriceTable PriceTable { get; set; } = null!;
    public virtual ICollection<PriceEntry> PriceEntries { get; set; } = new List<PriceEntry>();
    public virtual ICollection<PriceVariantArea> PriceVariantAreas { get; set; } = new List<PriceVariantArea>();
}
