using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

/// <summary>
///     Unit of measurement lookup. Conversion between Uoms is per-item (not global),
///     see ItemUomConversion (Area H) — "chai" can mean 750ml for fish sauce vs
///     330ml for beer. Also referenced by CartItem/OrderItem (Area E).
/// </summary>
public class Uom : Entity
{
    public int Id { get; set; }

    /// <summary>Machine-readable code: kg, g, l, ml, chai, lon, to, phan. Unique.</summary>
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();
}
