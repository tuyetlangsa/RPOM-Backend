using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Domain.Sales;

/// <summary>
/// Mutable cart line in a DRAFT order. Snapshots (ItemCode/ItemName/UnitPrice/
/// UomCode/UomName) preserve historical accuracy. On Order DRAFT → SENT, copied
/// to OrderItem then deleted. Concurrency for QR shared cart: last-write-wins
/// (no Version).
/// </summary>
public class CartItem : Entity
{
    public long Id { get; set; }

    /// <summary>Parent order — must be Status=DRAFT to mutate.</summary>
    public long OrderId { get; set; }

    public int ItemId { get; set; }

    /// <summary>Snapshot of Item.Code at order time.</summary>
    public string ItemCode { get; set; } = null!;

    /// <summary>Snapshot of Item.Name at order time.</summary>
    public string ItemName { get; set; } = null!;
    public int UomId { get; set; }

    /// <summary>Snapshot of Uom.Code at order time.</summary>
    public string UomCode { get; set; } = null!;

    /// <summary>Snapshot of Uom.Name at order time.</summary>
    public string UomName { get; set; } = null!;

    public decimal Quantity { get; set; } = 1;

    /// <summary>Snapshot from active PriceEntry at moment of add.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Quantity × UnitPrice + Σ CartItemDetail.ExtraPrice × Quantity. Denormalized.</summary>
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Poll cursor — QR shared cart sync across devices on same ticket.</summary>
    public DateTime UpdatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;
    public virtual Item Item { get; set; } = null!;
    public virtual Uom Uom { get; set; } = null!;
    public virtual ICollection<CartItemDetail> Details { get; set; } = new List<CartItemDetail>();
}
