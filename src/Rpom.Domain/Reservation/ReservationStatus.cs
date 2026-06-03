namespace Rpom.Domain.Reservation;

/// <summary>
/// Reservation.Status.
/// <para>
/// Transitions:
/// BOOKED → ARRIVED   (customer arrives; cashier creates ticket → set LinkedTicketId)
/// BOOKED → CANCELLED (cashier cancels — customer cancels OR no-show, reason required)
/// </para>
/// <para>
/// ARRIVED and CANCELLED are terminal. NO_SHOW is NOT a separate state —
/// when customer doesn't arrive, cashier manually cancels with reason "không đến".
/// </para>
/// <para>HOLDING is NOT stored — derived at render-time from BOOKED + time window.</para>
/// </summary>
public static class ReservationStatus
{
    public const string Booked = "BOOKED";
    public const string Arrived = "ARRIVED";
    public const string Cancelled = "CANCELLED";
}
