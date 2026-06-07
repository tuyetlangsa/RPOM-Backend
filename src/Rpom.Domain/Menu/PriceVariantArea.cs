using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Domain.Menu;

/// <summary>
///     Junction PriceVariant ↔ Area for area-specific variant applicability.
///     Only meaningful when PriceVariant.AppliesToAllAreas = false.
///     Composite PK (PriceVariantId, AreaId).
/// </summary>
public class PriceVariantArea : Entity
{
    public int PriceVariantId { get; set; }

    /// <summary>FK to Area (Area B).</summary>
    public int AreaId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual PriceVariant PriceVariant { get; set; } = null!;
    public virtual Area Area { get; set; } = null!;
}
