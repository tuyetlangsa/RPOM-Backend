using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

/// <summary>
///     IMMUTABLE snapshot of the final bill at ticket close time.
///     Created ONCE when ticket transitions OPEN -> CLOSED within CloseTicket handler.
///     Never updated or deleted after creation — source of truth for all reports.
///     1:1 with Ticket (one invoice per closed ticket).
/// </summary>
public class TicketInvoice : Entity
{
    public long Id { get; set; }

    /// <summary>FK to Ticket.Id. 1:1 — one invoice per closed ticket.</summary>
    public long TicketId { get; set; }

    // --- Header fields (snapshots from Ticket at close time) ---
    public string TicketCode { get; set; } = null!;
    public int CounterId { get; set; }
    public int AreaId { get; set; }
    public int ShiftId { get; set; }
    public int TableId { get; set; }
    public string TableCode { get; set; } = null!;
    public short GuestCount { get; set; }
    public int? WaiterStaffId { get; set; }
    public string? WaiterName { get; set; }
    public int? ClosedByStaffId { get; set; }
    public string? ClosedByName { get; set; }

    // --- Money snapshots (copied from Ticket at close) ---
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal ServiceChargePercent { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RoundingAdjustment { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal ChangeAmount { get; set; }

    // --- Timestamps ---
    public DateTime OpenedAt { get; set; }
    public DateTime ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // --- Navigation ---
    public virtual Ticket Ticket { get; set; } = null!;
    public virtual ICollection<TicketInvoiceLine> Lines { get; set; } = new List<TicketInvoiceLine>();
}
