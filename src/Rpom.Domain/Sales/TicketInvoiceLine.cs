using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

/// <summary>
///     IMMUTABLE line item within a TicketInvoice.
///     Each row corresponds to one TicketItemSum bucket at close time.
///     Never updated or deleted after creation.
/// </summary>
public class TicketInvoiceLine : Entity
{
    public long Id { get; set; }
    public long TicketInvoiceId { get; set; }

    public int ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string UomCode { get; set; } = null!;
    public string UomName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public decimal ChoicePricePerUnit { get; set; }
    public decimal Quantity { get; set; }
    public decimal VatPercent { get; set; }
    public decimal ServiceChargePercent { get; set; }
    public decimal ServiceChargeVatPercent { get; set; }
    public decimal LineDiscountPercent { get; set; }
    public decimal TicketDiscountPercent { get; set; }
    public decimal LineSubtotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual TicketInvoice TicketInvoice { get; set; } = null!;
}
