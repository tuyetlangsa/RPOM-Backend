namespace Rpom.Domain.Inventory;

/// <summary>StockMovement.ReferenceType values for the polymorphic source reference.</summary>
public static class StockMovementReferenceType
{
    /// <summary>DEDUCT triggered by OrderItem; ReferenceId = OrderItem.Id.</summary>
    public const string OrderDish = "ORDER_DISH";

    /// <summary>Manual movement by Owner (STOCK_IN, ADJUST_*); ReferenceId = NULL.</summary>
    public const string Manual = "MANUAL";
}
