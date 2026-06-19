using Rpom.Domain.Inventory;

namespace Rpom.Application.Abstraction.Inventory;

/// <summary>
///     Inventory ledger service. All stock mutations go through this service
///     to guarantee the StockMovement ledger + ItemStock cache stay in sync.
///     Mutations are called inside the caller's ambient transaction.
/// </summary>
public interface IStockMovementService
{
    /// <summary>Create a manual movement (STOCK_IN / ADJUST_IN / ADJUST_OUT) + update ItemStock.</summary>
    Task<StockMovement> CreateManualAsync(int itemId, string movementType, decimal signedQty,
        string? reason, int staffId, DateTime now, CancellationToken ct);

    /// <summary>
    ///     Auto-DEDUCT triggered by OrderItem entering PROCESSING.
    ///     If the item HasRecipe, each active BomLine material is deducted.
    ///     If IsStockable &amp;&amp; !HasRecipe, the item itself is deducted.
    ///     Non-stockable items are silently skipped.
    /// </summary>
    Task DeductAsync(long orderItemId, int staffId, CancellationToken ct);

    /// <summary>
    ///     Reverse a deduction back into stock for a kitchen-processed RETURN line
    ///     (negative-quantity refund OrderItem). Mirrors <see cref="DeductAsync"/> but ADDS:
    ///     HasRecipe → adds each BOM material back; IsStockable &amp;&amp; !HasRecipe → adds the
    ///     item itself back. Amount = |refund OrderItem.Quantity|. Idempotent — a second call
    ///     for the same refund line is a no-op (guarded by an existing ORDER_RETURN movement).
    /// </summary>
    Task RestockReturnAsync(long refundOrderItemId, int staffId, CancellationToken ct);
}
