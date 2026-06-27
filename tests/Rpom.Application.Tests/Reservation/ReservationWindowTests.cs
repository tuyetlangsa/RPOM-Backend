using FluentAssertions;
using Rpom.Application.Reservation;

namespace Rpom.Application.Tests.Reservation;

public sealed class ReservationWindowTests
{
    private static readonly DateTime T18 = new(2026, 6, 27, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsHeld_InsideWindow_True()
    {
        ReservationWindow.IsHeld(T18, 30, 30, T18.AddMinutes(-10)).Should().BeTrue();
        ReservationWindow.IsHeld(T18, 30, 30, T18.AddMinutes(20)).Should().BeTrue();
    }

    [Fact]
    public void IsHeld_OutsideWindow_False()
    {
        ReservationWindow.IsHeld(T18, 30, 30, T18.AddMinutes(-31)).Should().BeFalse();
        ReservationWindow.IsHeld(T18, 30, 30, T18.AddMinutes(31)).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_IntervalNotPoint()
    {
        // existing 18:00 window [17:30,18:30]; new 18:45 window [18:15,19:15] -> overlap at [18:15,18:30]
        ReservationWindow.Overlaps(T18, T18.AddMinutes(45), 30, 30).Should().BeTrue();
        // new 19:01 window [18:31,19:31] does NOT touch [17:30,18:30]
        ReservationWindow.Overlaps(T18, T18.AddMinutes(61), 30, 30).Should().BeFalse();
    }

    [Fact]
    public void Phase_Transitions()
    {
        ReservationWindow.Phase(T18, 30, 30, T18.AddMinutes(-31)).Should().Be("PENDING");
        ReservationWindow.Phase(T18, 30, 30, T18.AddMinutes(0)).Should().Be("HOLDING");
        ReservationWindow.Phase(T18, 30, 30, T18.AddMinutes(31)).Should().Be("EXPIRED");
    }
}
