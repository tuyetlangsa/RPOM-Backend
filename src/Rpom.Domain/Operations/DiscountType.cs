namespace Rpom.Domain.Operations;

/// <summary>DiscountPolicy.DiscountType discriminator values.</summary>
public static class DiscountType
{
    /// <summary>Triggered by total ticket amount; discount distributed across line items.</summary>
    public const string TicketThreshold = "TICKET_THRESHOLD";

    /// <summary>Triggered by quantity of a specific item; discount applies to matching item only.</summary>
    public const string QuantityItem = "QUANTITY_ITEM";
}
