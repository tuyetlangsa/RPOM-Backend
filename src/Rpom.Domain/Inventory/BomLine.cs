using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Domain.Inventory;

/// <summary>
/// Recipe line. Self-ref junction Item ↔ Item — defines "to make 1 SellableItem,
/// consume Quantity of MaterialItem". App-enforced: SellableItem.HasRecipe=true,
/// MaterialItem.IsStockable=true, no self-loop, no recipe cycles.
/// Triggered DEDUCT on OrderItem PENDING → PROCESSING transition.
/// </summary>
public class BomLine : Entity
{
    public int Id { get; set; }

    /// <summary>Item with HasRecipe=true that consumes this material when sold.</summary>
    public int SellableItemId { get; set; }

    /// <summary>Item with IsStockable=true that gets deducted when SellableItem is processed.</summary>
    public int MaterialItemId { get; set; }

    /// <summary>Amount of MaterialItem consumed per 1 unit of SellableItem (in UomId below).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit for Quantity — must be MaterialItem.BaseUomId or in MaterialItem's ItemUomConversion.</summary>
    public int UomId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Item SellableItem { get; set; } = null!;
    public virtual Item MaterialItem { get; set; } = null!;
    public virtual Uom Uom { get; set; } = null!;
}
