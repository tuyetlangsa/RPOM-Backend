using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

/// <summary>
///     e-Invoice (Hoá đơn điện tử) request. 1:1 specialization of Ticket —
///     PK = FK = TicketId. Existence of row marks Ticket as having a VAT invoice
///     request. v1: data captured only; VNPT/MISA/Viettel integration out of scope.
/// </summary>
public class EInvoice : Entity
{
    /// <summary>PK = FK to Ticket.Id. 1:1 specialization.</summary>
    public long TicketId { get; set; }

    public string CustomerName { get; set; } = null!;

    /// <summary>Mã số thuế của khách (10 hoặc 13 ký tự).</summary>
    public string? TaxCode { get; set; }

    public string? Address { get; set; }
    public string? Email { get; set; }

    /// <summary>When invoice generated/printed. NULL while pending.</summary>
    public DateTime? IssuedAt { get; set; }

    /// <summary>Assigned by VNPT/MISA/Viettel integration (future). NULL in v1.</summary>
    public string? ExternalInvoiceNumber { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Ticket Ticket { get; set; } = null!;
}
