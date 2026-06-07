using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Domain.Sales;

/// <summary>
///     Mutable cart line in a DRAFT order. Snapshots (ItemCode/ItemName/UnitPrice/
///     UomCode/UomName) preserve historical accuracy. On Order DRAFT → SENT, copied
///     to OrderItem then deleted. Concurrency for QR shared cart: last-write-wins
///     (no Version).
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

    /// <summary>Σ CartItemDetail.ExtraPrice per unit (pre-VAT).</summary>
    public decimal ChoicePricePerUnit { get; set; }

    /// <summary>Snapshot Item.VatPercent at add.</summary>
    public decimal VatPercent { get; set; }

    /// <summary>Snapshot Ticket.ServiceChargePercent.</summary>
    public decimal ServiceChargePercent { get; set; }

    /// <summary>Snapshot Ticket.ServiceChargeVatPercent.</summary>
    public decimal ServiceChargeVatPercent { get; set; }

    /// <summary>Quantity × (UnitPrice + ChoicePricePerUnit), rounded.</summary>
    public decimal LineSubtotal { get; set; }

    /// <summary>LineSubtotal × ServiceChargePercent / 100.</summary>
    public decimal ServiceChargeAmount { get; set; }

    /// <summary>LineSubtotal × VatPercent / 100.</summary>
    public decimal VatItemAmount { get; set; }

    /// <summary>ServiceChargeAmount × ServiceChargeVatPercent / 100.</summary>
    public decimal VatScAmount { get; set; }

    /// <summary>VatItemAmount + VatScAmount.</summary>
    public decimal VatAmount { get; set; }

    /// <summary>LineSubtotal + ServiceChargeAmount + VatAmount (all-in tạm tính).</summary>
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
