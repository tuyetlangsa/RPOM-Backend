using System.Collections.ObjectModel;
using Rpom.Domain.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;

namespace Rpom.Domain.Reservation;

/// <summary>
///     Phone-booking record. Customer is anonymous (no Customer master — Glossary §9).
///     Books one or more tables (<see cref="ReservationTables" />), all in one counter.
///     Hold is derived at read time: a table is held iff a BOOKED reservation covers it and
///     now ∈ [TargetTime − pre_buffer, TargetTime + grace_period]. No placeholder ticket.
/// </summary>
public class Reservation : Entity
{
    public long Id { get; set; }

    /// <summary>Auto-generated business code, e.g. "R-2026-123".</summary>
    public string Code { get; set; } = null!;

    /// <summary>Denormalized owning counter — all booked tables share it. Mirrors Ticket.CounterId.</summary>
    public int CounterId { get; set; }

    public string CustomerName { get; set; } = null!;
    public string CustomerPhone { get; set; } = null!;
    public short GuestCount { get; set; } = 1;
    public string? Note { get; set; }

    /// <summary>When the customer is expected. Drives the hold window.</summary>
    public DateTime TargetTime { get; set; }

    /// <summary>BOOKED | ARRIVED | CANCELLED | NOT_ARRIVED (see <see cref="ReservationStatus" />).</summary>
    public string Status { get; set; } = ReservationStatus.Booked;

    public DateTime? ArrivedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? CancellationReasonId { get; set; }
    public string? CancellationNote { get; set; }

    /// <summary>Staff who took the booking call.</summary>
    public int CreatedByStaffId { get; set; }

    /// <summary>Optimistic concurrency token (Cancel/Seat races). EF IsConcurrencyToken.</summary>
    public int Version { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Poll cursor — reservation list + floor-plan projection refresh.</summary>
    public DateTime UpdatedAt { get; set; }

    public virtual Counter Counter { get; set; } = null!;
    public virtual CancellationReason? CancellationReason { get; set; }
    public virtual StaffAccount CreatedByStaff { get; set; } = null!;

    /// <summary>The tables this reservation books (booking intent — overlap/projection/hold).</summary>
    public virtual ICollection<ReservationTable> ReservationTables { get; set; } =
        new Collection<ReservationTable>();
}
