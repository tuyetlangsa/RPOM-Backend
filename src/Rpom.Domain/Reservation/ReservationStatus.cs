namespace Rpom.Domain.Reservation;

/// <summary>
///     Reservation.Status.
///     <para>
///         Transitions:
///         BOOKED → ARRIVED      (customer arrives; cashier opens ticket via Seat flow)
///         BOOKED → CANCELLED    (cashier cancels — customer cancels OR no-show, reason required)
///         BOOKED → NOT_ARRIVED  (set lazily on read when window has expired and status still BOOKED)
///     </para>
///     <para>
///         ARRIVED and CANCELLED are terminal. NOT_ARRIVED is set lazily on list read —
///         never stored as a manual transition.
///     </para>
///     <para>HOLDING is NOT stored — derived at render-time from BOOKED + time window.</para>
/// </summary>
public static class ReservationStatus
{
    public const string Booked = "BOOKED";
    public const string Arrived = "ARRIVED";
    public const string Cancelled = "CANCELLED";

    /// <summary>Past window_end while still BOOKED; set lazily on read of the list.</summary>
    public const string NotArrived = "NOT_ARRIVED";
}
