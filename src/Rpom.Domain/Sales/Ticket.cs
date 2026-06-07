using Rpom.Domain.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;
using Rpom.Domain.Restaurant;

namespace Rpom.Domain.Sales;

/// <summary>
/// Hoá đơn — central transaction entity. Hub: referenced by Order,
/// TicketPaymentDetail, EInvoice (1:1), Reservation (Area F when SEATED).
/// Money fields are computed snapshots; while OPEN app recomputes on each
/// change but only persists final values at payment.
/// </summary>
public class Ticket : Entity
{
    public long Id { get; set; }

    /// <summary>Owner-defined or auto-generated business code (e.g. "T-2026-001-Q1").</summary>
    public string Code { get; set; } = null!;

    public int TableId { get; set; }

    /// <summary>DENORM from Table → Area for fast "tickets in area X" queries.</summary>
    public int AreaId { get; set; }

    /// <summary>DENORM from Area → Counter for fast "tickets at counter X" queries.</summary>
    public int CounterId { get; set; }

    /// <summary>Active cash drawer at the counter when ticket opened (cashier audit).</summary>
    public long CashDrawerSessionId { get; set; }

    /// <summary>
    /// DENORM from Shift definition resolved by ticket open time-of-day —
    /// for reporting "doanh thu ca sáng". Independent from cash drawer.
    /// </summary>
    public int ShiftId { get; set; }

    public short GuestCount { get; set; } = 1;

    /// <summary>Order Staff who took the ticket. NULL for QR self-order (Guest actor).</summary>
    public int? WaiterStaffId { get; set; }

    /// <summary>Manager who approved exceptions (reopen, discount over threshold). NULL when not needed.</summary>
    public int? ManagerStaffId { get; set; }

    /// <summary>OPEN | CLOSED | CANCELLED (see <see cref="TicketStatus"/>).</summary>
    public string Status { get; set; } = TicketStatus.Open;
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>Required when CancelledAt is set.</summary>
    public int? CancellationReasonId { get; set; }
    public string? CancellationNote { get; set; }

    /// <summary>Σ OrderItem.LineSubtotal — gross, before Discount/SC/VAT (pricing spec §3.7).</summary>
    public decimal Subtotal { get; set; }
    public int? DiscountPolicyId { get; set; }

    /// <summary>Ticket-level discount % the cashier set / a policy resolved.</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>Total discount applied (distributed across line items).</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Snapshot from Area.ServiceChargePercent at open; re-snapshot on table transfer (pricing spec §3.7, CLAUDE.md §14).</summary>
    public decimal ServiceChargePercent { get; set; }
    public decimal ServiceChargeAmount { get; set; }

    /// <summary>Snapshot from Area.ServiceChargeVatPercent at open (pricing spec §3.7).</summary>
    public decimal ServiceChargeVatPercent { get; set; }

    /// <summary>Σ OrderItem.LineDiscountAmount.</summary>
    public decimal LineDiscountTotal { get; set; }

    /// <summary>Σ OrderItem.TicketDiscountAmount.</summary>
    public decimal TicketDiscountTotal { get; set; }

    /// <summary>Legacy header VAT% — kept for back-compat; NOT used by recompute (VAT is per-line via OrderItem.VatPercent).</summary>
    public decimal VatPercent { get; set; }
    public decimal VatAmount { get; set; }

    /// <summary>Subtotal - DiscountAmount + ServiceChargeAmount + VatAmount.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Rounding error: TotalAmount − (Subtotal − Discount + SC + VAT). Printed as "Làm tròn ±X".</summary>
    public decimal RoundingAdjustment { get; set; }

    /// <summary>Σ TicketPaymentDetail.Amount for SUCCESS rows.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>MAX(0, PaidAmount − TotalAmount) — overpay to be refunded.</summary>
    public decimal RefundAmount { get; set; }

    /// <summary>Cash change given back (only relevant when overpaid in cash).</summary>
    public decimal ChangeAmount { get; set; }

    /// <summary>Opaque 20-char token for QR self-order. NULL when not enabled. Unique when set.</summary>
    public string? GuestQrToken { get; set; }
    public DateTime? GuestQrGeneratedAt { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Poll cursor — Ticket Detail screen + Active Tickets list realtime refresh.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency — multi-staff edit (waiter + cashier + manager simultaneous).</summary>
    public int Version { get; set; }

    public virtual Table Table { get; set; } = null!;
    public virtual Area Area { get; set; } = null!;
    public virtual Counter Counter { get; set; } = null!;
    public virtual CashDrawer.CashDrawerSession CashDrawerSession { get; set; } = null!;
    public virtual Shift Shift { get; set; } = null!;
    public virtual StaffAccount? WaiterStaff { get; set; }
    public virtual StaffAccount? ManagerStaff { get; set; }
    public virtual CancellationReason? CancellationReason { get; set; }
    public virtual DiscountPolicy? DiscountPolicy { get; set; }
    public virtual EInvoice? EInvoice { get; set; }
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    public virtual ICollection<TicketPaymentDetail> Payments { get; set; } = new List<TicketPaymentDetail>();
    public virtual ICollection<TicketItemSum> ItemSums { get; set; } = new List<TicketItemSum>();
}
