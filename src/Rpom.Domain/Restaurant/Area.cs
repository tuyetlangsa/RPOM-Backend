using Rpom.Domain.Common;

namespace Rpom.Domain.Restaurant;

/// <summary>
/// Khu vực within a Counter — groups Tables that share a service area.
/// FLAT list inside Counter — no nested Areas, no parent_id self-ref.
/// Cross-area "Area menu-shows Categories" resolved via AreaMenuCategory (Area C).
/// </summary>
public class Area : Entity
{
    public int Id { get; set; }

    /// <summary>Parent counter — Area belongs to exactly 1 Counter.</summary>
    public int CounterId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public short DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>SC% applied to tickets opened in this area (pricing spec §3.1).</summary>
    public decimal ServiceChargePercent { get; set; }

    /// <summary>VAT% applied to the service-charge amount.</summary>
    public decimal ServiceChargeVatPercent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Counter Counter { get; set; } = null!;
    public virtual ICollection<Table> Tables { get; set; } = new List<Table>();
}
