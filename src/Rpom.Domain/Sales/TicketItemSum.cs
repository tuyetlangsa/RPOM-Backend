using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Domain.Sales;

/// <summary>
/// Pre-aggregated summary of OrderItem per Ticket — F2 RESTICKETITEM_SUM pattern.
/// Source of truth for bill display, print, and VAT invoice line aggregation.
/// Each row = unique bucket (Ticket × Item × Uom × UnitPrice × DiscountPercent
/// × ChoicePricePerUnit × VatPercent × ServiceChargePercent).
/// Maintained by app-layer TicketSumRecomputer (DELETE + INSERT per ticket).
/// </summary>
public class TicketItemSum : Entity
{
    public long Id { get; set; }
    public long TicketId { get; set; }

    public int ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public int UomId { get; set; }

    public decimal UnitPrice { get; set; }

    /// <summary>Distributed discount % from Ticket-level DiscountPolicy.</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>Sum of modifier ExtraPrices per unit — signature for modifier bucket.</summary>
    public decimal ChoicePricePerUnit { get; set; }
    public decimal VatPercent { get; set; }
    public decimal ServiceChargePercent { get; set; }

    public decimal TotalQuantity { get; set; }

    /// <summary>TotalQuantity × ChoicePricePerUnit.</summary>
    public decimal TotalChoiceAmount { get; set; }

    /// <summary>(UnitPrice + ChoicePricePerUnit) × TotalQuantity before line discount.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Subtotal × DiscountPercent / 100.</summary>
    public decimal TotalDiscount { get; set; }
    public decimal TotalVat { get; set; }

    /// <summary>Final amount for this aggregated bucket.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Latest OrderItem.Id contributing — for "what changed last" + JOIN back for modifier display.</summary>
    public long MaxOrderItemId { get; set; }

    /// <summary>Render order on bill — populated from MIN(OrderItem.SentAt) rank at recompute time.</summary>
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Ticket Ticket { get; set; } = null!;
    public virtual Item Item { get; set; } = null!;
    public virtual Uom Uom { get; set; } = null!;
}
