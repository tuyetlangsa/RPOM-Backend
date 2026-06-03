using Rpom.Domain.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;
using Rpom.Domain.Restaurant;

namespace Rpom.Domain.Sales;

/// <summary>
/// Generic work-shift session for ANY staff role (cashier, order, manager, kitchen).
/// Each staff must clock in to start using the app.
/// <para>
/// Scope is XOR: either a Counter (service-facing roles) or a KitchenStation
/// (kitchen staff). App-enforced: exactly one of CounterId / KitchenStationId is set.
/// </para>
/// <para>
/// Cash tracking columns (OpeningCash, Variance, ...) are populated only when
/// HasCashTracking=true (cashier role with a cash drawer). NULL otherwise.
/// </para>
/// <para>
/// Constraints (enforced at DB):
/// - 1 OPEN session per StaffAccountId at a time (filtered unique).
/// - 1 OPEN session per CounterId where HasCashTracking=true (1 cash drawer per counter).
/// - Multiple kitchen staff CAN OPEN the same KitchenStation concurrently.
/// </para>
/// </summary>
public class ShiftSession : Entity
{
    public long Id { get; set; }

    /// <summary>Which shift definition (Area D) this session instantiates.</summary>
    public int ShiftId { get; set; }

    /// <summary>Staff opening this session. Any role: cashier, order, manager, kitchen.</summary>
    public int StaffAccountId { get; set; }

    /// <summary>XOR with KitchenStationId. Set for service-facing roles (cashier, order, manager).</summary>
    public int? CounterId { get; set; }

    /// <summary>XOR with CounterId. Set for kitchen staff.</summary>
    public int? KitchenStationId { get; set; }

    /// <summary>
    /// True only for cashier sessions — drives whether cash columns are populated and
    /// whether the "1 OPEN per Counter" constraint applies.
    /// </summary>
    public bool HasCashTracking { get; set; }

    public DateTime OpenedAt { get; set; }

    /// <summary>NULL while session is OPEN.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Sum of CashCount Phase=OPENING subtotals. NULL when HasCashTracking=false.</summary>
    public decimal? OpeningCash { get; set; }

    /// <summary>OpeningCash + Σ cash payments - Σ change. Computed at close. NULL when non-cashier.</summary>
    public decimal? ExpectedClosingCash { get; set; }

    /// <summary>Sum of CashCount Phase=CLOSING subtotals. NULL when non-cashier.</summary>
    public decimal? ActualClosingCash { get; set; }

    /// <summary>ActualClosingCash - ExpectedClosingCash. Logged only. NULL when non-cashier.</summary>
    public decimal? Variance { get; set; }

    /// <summary>OPEN | CLOSED (see <see cref="ShiftSessionStatus"/>).</summary>
    public string Status { get; set; } = ShiftSessionStatus.Open;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Poll cursor — staff dashboard refresh.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency — guards "open ca while one already open" race.</summary>
    public int Version { get; set; }

    public virtual Shift Shift { get; set; } = null!;
    public virtual StaffAccount Staff { get; set; } = null!;
    public virtual Counter? Counter { get; set; }
    public virtual KitchenStation? KitchenStation { get; set; }
    public virtual ICollection<ShiftSessionCashCount> CashCounts { get; set; } = new List<ShiftSessionCashCount>();
    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
