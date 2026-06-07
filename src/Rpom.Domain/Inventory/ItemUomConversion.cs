using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Domain.Inventory;

/// <summary>
///     Per-item alternative unit conversion. Each row: "for Item X, 1 unit of Uom U
///     equals FactorToBase × baseUom". The base unit itself is NOT in this table
///     (factor 1 implicit). Why per-item: "chai" means 750ml for fish sauce vs 330ml
///     for beer — conversion depends on the specific item.
/// </summary>
public class ItemUomConversion : Entity
{
    public int Id { get; set; }

    /// <summary>Owner Item — conversion is scoped to THIS item only.</summary>
    public int ItemId { get; set; }

    /// <summary>Alternative unit (gam, bao, ml, thùng) — different from Item.BaseUomId.</summary>
    public int UomId { get; set; }

    /// <summary>1 [Uom] = FactorToBase × Item.BaseUomId. e.g. for Bò (base=kg): gam → 0.001; bao → 25.</summary>
    public decimal FactorToBase { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Item Item { get; set; } = null!;
    public virtual Uom Uom { get; set; } = null!;
}
