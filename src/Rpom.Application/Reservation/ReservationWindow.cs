namespace Rpom.Application.Reservation;

/// <summary>
///     Pure hold-window math. window = [TargetTime - preBuffer, TargetTime + grace].
///     Used by Create (overlap), List (phase + lazy-expire), FloorPlan + Projection (held).
/// </summary>
public static class ReservationWindow
{
    public static (DateTime Start, DateTime End) Compute(DateTime target, int preBufferMinutes, int graceMinutes)
        => (target.AddMinutes(-preBufferMinutes), target.AddMinutes(graceMinutes));

    public static bool IsHeld(DateTime target, int preBufferMinutes, int graceMinutes, DateTime now)
    {
        (DateTime start, DateTime end) = Compute(target, preBufferMinutes, graceMinutes);
        return start <= now && now <= end;
    }

    /// <summary>True when the two target times' hold windows intersect (interval overlap, BR-R1).</summary>
    public static bool Overlaps(DateTime targetA, DateTime targetB, int preBufferMinutes, int graceMinutes)
    {
        (DateTime aStart, DateTime aEnd) = Compute(targetA, preBufferMinutes, graceMinutes);
        (DateTime bStart, DateTime bEnd) = Compute(targetB, preBufferMinutes, graceMinutes);
        return aStart <= bEnd && bStart <= aEnd;
    }

    /// <summary>Derived UI phase of a BOOKED reservation: PENDING | HOLDING | EXPIRED.</summary>
    public static string Phase(DateTime target, int preBufferMinutes, int graceMinutes, DateTime now)
    {
        (DateTime start, DateTime end) = Compute(target, preBufferMinutes, graceMinutes);
        if (now < start) return "PENDING";
        return now <= end ? "HOLDING" : "EXPIRED";
    }
}
