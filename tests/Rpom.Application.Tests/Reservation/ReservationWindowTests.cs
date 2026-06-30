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

    [Fact]
    public void Compute_ReturnsExpectedBounds()
    {
        var (start, end) = ReservationWindow.Compute(T18, 30, 30);
        start.Should().Be(T18.AddMinutes(-30)); // 17:30
        end.Should().Be(T18.AddMinutes(30));    // 18:30
    }

    [Fact]
    public void IsHeld_ExactlyAtStartBoundary_True()
    {
        // start = T18 - 30 min = 17:30; inclusive lower bound
        DateTime start = T18.AddMinutes(-30);
        ReservationWindow.IsHeld(T18, 30, 30, start).Should().BeTrue();
    }

    [Fact]
    public void IsHeld_ExactlyAtEndBoundary_True()
    {
        // end = T18 + 30 min = 18:30; inclusive upper bound
        DateTime end = T18.AddMinutes(30);
        ReservationWindow.IsHeld(T18, 30, 30, end).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_AdjacentTouchingWindows_True()
    {
        // targetA=18:00 → A=[17:30,18:30]; targetB=17:00 → B=[16:30,17:30]
        // Touch at single point 17:30. aStart(17:30) <= bEnd(17:30) && bStart(16:30) <= aEnd(18:30) → true.
        ReservationWindow.Overlaps(T18, T18.AddMinutes(-60), 30, 30).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_Disjoint_False()
    {
        // targetA=18:00 → A=[17:30,18:30]; targetB=16:00 → B=[15:30,16:30]
        // aStart(17:30) <= bEnd(16:30) is false → no overlap.
        ReservationWindow.Overlaps(T18, T18.AddMinutes(-120), 30, 30).Should().BeFalse();
    }

    [Fact]
    public void Phase_ExactBoundaries()
    {
        DateTime start = T18.AddMinutes(-30); // 17:30
        DateTime end   = T18.AddMinutes(30);  // 18:30

        ReservationWindow.Phase(T18, 30, 30, start).Should().Be("HOLDING");            // now == start
        ReservationWindow.Phase(T18, 30, 30, end).Should().Be("HOLDING");              // now == end
        ReservationWindow.Phase(T18, 30, 30, start.AddTicks(-1)).Should().Be("PENDING"); // one tick before start
        ReservationWindow.Phase(T18, 30, 30, end.AddTicks(1)).Should().Be("EXPIRED");    // one tick after end
    }
}
