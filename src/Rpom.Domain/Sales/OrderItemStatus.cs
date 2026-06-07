namespace Rpom.Domain.Sales;

/// <summary>
///     OrderItem.Status — kitchen lifecycle.
///     <para>
///         Transitions:
///         PENDING → PROCESSING (kitchen staff starts cooking)
///         PENDING → CANCELLED  (order staff cancels — typically out-of-stock; ONLY allowed before PROCESSING)
///         PROCESSING → READY   (kitchen staff marks dish ready)
///         READY → DONE         (order staff serves dish and marks done)
///     </para>
///     <para>
///         CANCELLED is only reachable from PENDING. Once kitchen has started cooking,
///         the dish CANNOT be cancelled. For damaged-dish refunds: create a NEW
///         OrderItem row with negative Quantity, referencing the original via
///         OriginalOrderItemId. The refund row runs through the same lifecycle.
///     </para>
///     <para>LATE is DERIVED at read-time (PROCESSING + elapsed > threshold), not stored.</para>
/// </summary>
public static class OrderItemStatus
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Ready = "READY";
    public const string Done = "DONE";
    public const string Cancelled = "CANCELLED";
}
