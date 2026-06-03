namespace Rpom.Domain.Sales;

/// <summary>
/// Ticket.Status.
/// <para>
/// Transitions:
/// OPEN → CLOSED    (cashier closes ticket; payment complete)
/// OPEN → CANCELLED (cancel bill with reason)
/// </para>
/// <para>CLOSED and CANCELLED are terminal — no reopen flow.</para>
/// </summary>
public static class TicketStatus
{
    public const string Open = "OPEN";
    public const string Closed = "CLOSED";
    public const string Cancelled = "CANCELLED";
}
