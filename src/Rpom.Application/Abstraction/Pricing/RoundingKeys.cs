namespace Rpom.Application.Abstraction.Pricing;

/// <summary>The 14 seeded rounding key codes (pricing spec §1). Closed set.</summary>
public static class RoundingKeys
{
    public const string PriceDetail = "I_ROUNDPRICEDETAIL";
    public const string MenuDisplay = "I_ROUNDMENUDISPLAY";
    public const string LineSubtotal = "I_ROUNDLINESUBTOTAL";
    public const string LineDiscount = "I_ROUNDLINEDISCOUNT";
    public const string LineSc = "I_ROUNDLINESC";
    public const string LineVatItem = "I_ROUNDLINEVATITEM";
    public const string LineVatSc = "I_ROUNDLINEVATSC";
    public const string LineTotal = "I_ROUNDLINETOTAL";
    public const string TicketSubtotal = "I_ROUNDTICKETSUBTOTAL";
    public const string TicketDiscount = "I_ROUNDTICKETDISCOUNT";
    public const string TicketSc = "I_ROUNDTICKETSC";
    public const string TicketVat = "I_ROUNDTICKETVAT";
    public const string TicketTotal = "I_ROUNDTICKETTOTAL";
    public const string TicketAdjust = "I_ROUNDTICKETADJUST";

    /// <summary>All 14 keys with their seeded default digit count.</summary>
    public static readonly IReadOnlyDictionary<string, short> Defaults =
        new Dictionary<string, short>
        {
            [PriceDetail] = 2, [MenuDisplay] = 0,
            [LineSubtotal] = 2, [LineDiscount] = 2,
            [LineSc] = 2, [LineVatItem] = 0,
            [LineVatSc] = 0, [LineTotal] = 0,
            [TicketSubtotal] = 2, [TicketDiscount] = 2,
            [TicketSc] = 2, [TicketVat] = 0,
            [TicketTotal] = 0, [TicketAdjust] = 2
        };
}
