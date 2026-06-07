namespace Rpom.Domain.Sales;

/// <summary>
///     Order.Status — rollup state machine, stored (not computed).
///     <para>
///         Transitions:
///         DRAFT → SENT (order staff click "Gửi bếp")
///         DRAFT → DELETED (order staff remove all dishes OR cashier closes ticket while DRAFT)
///         SENT → PROCESSING (kitchen staff bumps ≥1 child OrderItem to PROCESSING)
///         PROCESSING → DONE (all child OrderItems are DONE or CANCELLED, OR manually confirmed)
///     </para>
///     <para>
///         DONE and DELETED are terminal. App layer (handler) bumps Order.Status when
///         child OrderItem statuses change.
///     </para>
/// </summary>
public static class OrderStatus
{
    public const string Draft = "DRAFT";
    public const string Sent = "SENT";
    public const string Processing = "PROCESSING";
    public const string Done = "DONE";
    public const string Deleted = "DELETED";
}
