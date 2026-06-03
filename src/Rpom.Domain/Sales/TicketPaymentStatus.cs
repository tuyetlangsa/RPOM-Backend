namespace Rpom.Domain.Sales;

/// <summary>
/// TicketPaymentDetail.Status.
/// <para>
/// Transitions:
/// PENDING → SUCCESS   (cashier confirms payment / vendor callback confirms)
/// PENDING → CANCELLED (cashier cancels pending payment)
/// SUCCESS → DELETED   (cashier deletes the payment record — soft delete for audit)
/// </para>
/// <para>CANCELLED and DELETED are terminal.</para>
/// </summary>
public static class TicketPaymentStatus
{
    public const string Pending = "PENDING";
    public const string Success = "SUCCESS";
    public const string Cancelled = "CANCELLED";
    public const string Deleted = "DELETED";
}
