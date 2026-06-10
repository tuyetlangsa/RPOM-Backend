using Rpom.Domain.Access;
using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

/// <summary>
///     Single payment event against a Ticket. A Ticket can have multiple rows
///     (e.g. 200k cash + 100k QR = pay 300k in 2 txns).
///     Ticket.PaidAmount = Σ Amount WHERE Status=SUCCESS.
/// </summary>
public class TicketPaymentDetail : Entity
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public int PaymentMethodId { get; set; }

    /// <summary>Positive paid amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>PENDING | SUCCESS | CANCELLED | DELETED (see <see cref="TicketPaymentStatus" />).</summary>
    public string Status { get; set; } = TicketPaymentStatus.Pending;

    /// <summary>When transitioned to terminal status. NULL for INITIATED.</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>Cashier who initiated the transaction.</summary>
    public int ProcessedByStaffId { get; set; }

    /// <summary>Vendor transaction id for QR/Card; NULL for Cash.</summary>
    public string? TransactionRef { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Poll cursor — async QR vendor callback updates.</summary>
    public DateTime UpdatedAt { get; set; }

    public long? ParentPaymentDetailId { get; set; }

    public virtual Ticket Ticket { get; set; } = null!;
    public virtual PaymentMethod PaymentMethod { get; set; } = null!;
    public virtual StaffAccount ProcessedByStaff { get; set; } = null!;
    public virtual TicketPaymentDetail? ParentPaymentDetail { get; set; }
    public virtual ICollection<TicketPaymentDetail> ChildPaymentDetails { get; set; } = new List<TicketPaymentDetail>();
}
