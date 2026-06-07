using Rpom.Domain.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;

namespace Rpom.Domain.Reservation;

/// <summary>
///     Phone-booking record. Customer is anonymous (no Customer master — Glossary §9).
///     Hold derivation: table_is_held(T, now) iff EXISTS BOOKED Reservation on T
///     where now ∈ [TargetTime − pre_buffer, TargetTime + grace_period].
/// </summary>
public class Reservation : Entity
{
    public long Id { get; set; }

    /// <summary>Owner-defined or auto-generated business code (e.g. "R-2026-001").</summary>
    public string Code { get; set; } = null!;

    /// <summary>Specific table booked (Area B). Hold = derived from this row.</summary>
    public int TableId { get; set; }

    public string CustomerName { get; set; } = null!;

    /// <summary>Plain text — no customer DB lookup.</summary>
    public string CustomerPhone { get; set; } = null!;

    public short GuestCount { get; set; } = 1;

    /// <summary>Optional context: "birthday, need high chair", "vegetarian", ...</summary>
    public string? Note { get; set; }

    /// <summary>
    ///     When customer is expected to arrive. Hold window =
    ///     [TargetTime − pre_buffer, TargetTime + grace_period] from Reservation Config.
    /// </summary>
    public DateTime TargetTime { get; set; }

    /// <summary>BOOKED | ARRIVED | CANCELLED (see <see cref="ReservationStatus" />).</summary>
    public string Status { get; set; } = ReservationStatus.Booked;

    /// <summary>Set when customer arrives and ticket opened (BOOKED → ARRIVED).</summary>
    public DateTime? ArrivedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    /// <summary>Required when CancelledAt set (BR-CR1).</summary>
    public int? CancellationReasonId { get; set; }

    public string? CancellationNote { get; set; }

    /// <summary>Set when reservation becomes SEATED — the real Ticket opened. NULL while BOOKED.</summary>
    public long? LinkedTicketId { get; set; }

    /// <summary>Staff who took the booking call.</summary>
    public int CreatedByStaffId { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Poll cursor — Reservation list + Floor Plan projection refresh.</summary>
    public DateTime UpdatedAt { get; set; }

    public virtual Table Table { get; set; } = null!;
    public virtual CancellationReason? CancellationReason { get; set; }
    public virtual Ticket? LinkedTicket { get; set; }
    public virtual StaffAccount CreatedByStaff { get; set; } = null!;
}
