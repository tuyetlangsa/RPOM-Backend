using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

/// <summary>
/// Junction Area (Area B) ↔ Category. Composite PK (AreaId, CategoryId).
/// Including a parent Category implicitly includes ALL descendants (via Category.Path).
/// Replaces F2's denormalized MENUPATH varchar field.
/// </summary>
public class AreaMenuCategory : Entity
{
    /// <summary>FK to Area (Area B).</summary>
    public int AreaId { get; set; }
    public int CategoryId { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Restaurant.Area Area { get; set; } = null!;
    public virtual Category Category { get; set; } = null!;
}
