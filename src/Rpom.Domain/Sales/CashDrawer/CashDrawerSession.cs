using Rpom.Domain.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Domain.Sales.CashDrawer;

/// <summary>
///     Cash drawer session at a single Counter. Tracks opening / closing cash counts
///     and variance for that counter — independent of which staff member is on duty.
///     <para>
///         Lifecycle: OPEN → CLOSED (terminal). 1 OPEN drawer per Counter at any time
///         (filtered unique index on (CounterId, Status='OPEN')).
///     </para>
///     <para>
///         The staff who opens may be different from the staff who closes — both are
///         gated by separate permissions (<c>cash_drawer:open</c> / <c>cash_drawer:close</c>)
///         rather than role checks, so Owner/Manager can force-close on cashier behalf.
///     </para>
///     <para>
///         Personal check-in / clock-in is a separate concern handled by the future
///         StaffWorkSession entity — this entity is purely cash-tracking.
///     </para>
/// </summary>
public class CashDrawerSession : Entity
{
    public long Id { get; set; }

    /// <summary>Counter this drawer belongs to.</summary>
    public int CounterId { get; set; }

    /// <summary>Staff who opened the drawer (audit).</summary>
    public int OpenedByStaffAccountId { get; set; }

    public DateTime OpenedAt { get; set; }

    /// <summary>Sum of CashCount Phase=OPENING subtotals.</summary>
    public decimal OpeningCash { get; set; }

    /// <summary>Staff who closed the drawer. NULL while OPEN; can differ from opener.</summary>
    public int? ClosedByStaffAccountId { get; set; }

    public DateTime? ClosedAt { get; set; }

    /// <summary>OpeningCash + Σ cash payments - Σ change. Computed at close.</summary>
    public decimal? ExpectedClosingCash { get; set; }

    /// <summary>Sum of CashCount Phase=CLOSING subtotals.</summary>
    public decimal? ActualClosingCash { get; set; }

    /// <summary>ActualClosingCash - ExpectedClosingCash. Logged only — does not block close.</summary>
    public decimal? Variance { get; set; }

    /// <summary>OPEN | CLOSED (see <see cref="CashDrawerStatus" />).</summary>
    public string Status { get; set; } = CashDrawerStatus.Open;

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Poll cursor — cashier dashboard refresh.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency — guards "open drawer while one already open" race.</summary>
    public int Version { get; set; }

    public virtual Counter Counter { get; set; } = null!;
    public virtual StaffAccount OpenedByStaff { get; set; } = null!;
    public virtual StaffAccount? ClosedByStaff { get; set; }
    public virtual ICollection<CashDrawerCashCount> CashCounts { get; set; } = new List<CashDrawerCashCount>();
    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
