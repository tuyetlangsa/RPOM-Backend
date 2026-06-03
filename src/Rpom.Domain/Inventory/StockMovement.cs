using Rpom.Domain.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Domain.Inventory;

/// <summary>
/// Append-only inventory ledger. Rows INSERTed only, never UPDATEd/DELETEd.
/// QtyInBase is SIGNED; BalanceAfter = previous balance + QtyInBase.
/// DEDUCT no-reversal: once a dish enters PROCESSING, ingredients are consumed.
/// Cancellation later → manager posts compensating ADJUST_IN if appropriate;
/// the original DEDUCT row stays as immutable history.
/// </summary>
public class StockMovement : Entity
{
    public long Id { get; set; }

    /// <summary>Stockable item being moved (Item.IsStockable must be true).</summary>
    public int ItemId { get; set; }

    /// <summary>STOCK_IN | ADJUST_IN | ADJUST_OUT | DEDUCT (see <see cref="StockMovementType"/>).</summary>
    public string MovementType { get; set; } = null!;

    /// <summary>SIGNED in Item.BaseUomId. Positive for IN; negative for OUT/DEDUCT.</summary>
    public decimal QtyInBase { get; set; }

    /// <summary>Snapshot of ItemStock.CurrentQty after this row applied. Audit immutable.</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>ORDER_DISH (for DEDUCT) | MANUAL (for STOCK_IN/ADJUST_*) | NULL.</summary>
    public string? ReferenceType { get; set; }

    /// <summary>OrderItem.Id when ReferenceType=ORDER_DISH; NULL otherwise. NO FK (polymorphic).</summary>
    public long? ReferenceId { get; set; }

    /// <summary>Required for ADJUST_IN/ADJUST_OUT; optional for STOCK_IN; auto-filled for DEDUCT.</summary>
    public string? Reason { get; set; }
    public int CreatedByStaffId { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Item Item { get; set; } = null!;
    public virtual StaffAccount CreatedByStaff { get; set; } = null!;
}
