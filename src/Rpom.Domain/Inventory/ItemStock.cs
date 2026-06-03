using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Domain.Inventory;

/// <summary>
/// Materialized "current stock" per stockable Item. PK = FK = ItemId.
/// 1:0..1 with Item — only IsStockable=true items have a row.
/// Source of truth is StockMovement ledger; ItemStock is fast-read cache.
/// Both updated in the same transaction by StockMovementService.
/// </summary>
public class ItemStock : Entity
{
    /// <summary>PK = FK to Item.Id.</summary>
    public int ItemId { get; set; }

    /// <summary>Tồn hiện tại in Item.BaseUomId. Always = BalanceAfter of latest StockMovement.</summary>
    public decimal CurrentQty { get; set; }

    /// <summary>v1 optional (may stay 0). Reserved for sent-but-not-cooked orders. Available = CurrentQty - ReservedQty.</summary>
    public decimal ReservedQty { get; set; }

    /// <summary>Snapshot of latest StockMovement.CreatedAt — for "stale stock" dashboard alerts.</summary>
    public DateTime? LastMovementAt { get; set; }

    /// <summary>Poll cursor for stock dashboard + AI low-stock alert (Area I).</summary>
    public DateTime UpdatedAt { get; set; }

    public virtual Item Item { get; set; } = null!;
}
