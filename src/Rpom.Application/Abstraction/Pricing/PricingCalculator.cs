namespace Rpom.Application.Abstraction.Pricing;

/// <summary>Input for one line's pricing (pre-VAT, pre-SC unit price).</summary>
public readonly record struct LinePricingInput(
    decimal Quantity,
    decimal UnitPrice,
    decimal ChoicePricePerUnit,
    decimal VatPercent,
    decimal ServiceChargePercent,
    decimal ServiceChargeVatPercent,
    decimal LineDiscountPercent,
    decimal TicketDiscountPercent);

/// <summary>All computed money fields for one line.</summary>
public readonly record struct LinePricingResult(
    decimal LineSubtotal,
    decimal LineDiscountAmount,
    decimal TicketDiscountAmount,
    decimal TotalDiscountAmount,
    decimal ServiceChargeAmount,
    decimal VatItemAmount,
    decimal VatScAmount,
    decimal VatAmount,
    decimal LineTotal);

/// <summary>Computed ticket-header rollup.</summary>
public readonly record struct HeaderPricingResult(
    decimal Subtotal,
    decimal LineDiscountTotal,
    decimal TicketDiscountTotal,
    decimal DiscountAmount,
    decimal ServiceChargeAmount,
    decimal VatAmount,
    decimal TotalAmount,
    decimal RoundingAdjustment);

/// <summary>
///     Stateless pricing math — pricing spec §4. No DB, no clock. Both cart
///     (discount percentages = 0) and order lines use ComputeLine.
/// </summary>
public static class PricingCalculator
{
    public static LinePricingResult ComputeLine(LinePricingInput i, IRoundingConfig rc)
    {
        // Step 1 — gross line
        decimal lineSubtotal = Money.Round(
            i.Quantity * (i.UnitPrice + i.ChoicePricePerUnit), rc, RoundingKeys.LineSubtotal);

        // Step 2/3 — 1-cấp discount: bigger magnitude wins, loser zero. Percent-driven, so
        // negative-quantity (refund) lines self-scale and carry their proportional discount.
        decimal lineDiscRaw = lineSubtotal * i.LineDiscountPercent / 100m;
        decimal ticketDiscRaw = lineSubtotal * i.TicketDiscountPercent / 100m;

        decimal lineDiscount, ticketDiscount;
        if (Math.Abs(lineDiscRaw) >= Math.Abs(ticketDiscRaw))
        {
            lineDiscount = Money.Round(lineDiscRaw, rc, RoundingKeys.LineDiscount);
            ticketDiscount = 0m;
        }
        else
        {
            lineDiscount = 0m;
            ticketDiscount = Money.Round(ticketDiscRaw, rc, RoundingKeys.LineDiscount);
        }

        decimal totalDiscount = lineDiscount + ticketDiscount;

        // Step 4 — service charge on gross LineSubtotal
        decimal serviceCharge = Money.Round(
            lineSubtotal * i.ServiceChargePercent / 100m, rc, RoundingKeys.LineSc);

        // Step 5 — VAT of items, after discount
        decimal vatItem = Money.Round(
            (lineSubtotal - totalDiscount) * i.VatPercent / 100m, rc, RoundingKeys.LineVatItem);

        // Step 6 — VAT of service charge
        decimal vatSc = Money.Round(
            serviceCharge * i.ServiceChargeVatPercent / 100m, rc, RoundingKeys.LineVatSc);

        // Step 7 — total VAT
        decimal vat = vatItem + vatSc;

        // Step 8 — line total
        decimal lineTotal = Money.Round(
            lineSubtotal - totalDiscount + serviceCharge + vat, rc, RoundingKeys.LineTotal);

        return new LinePricingResult(
            lineSubtotal, lineDiscount, ticketDiscount, totalDiscount,
            serviceCharge, vatItem, vatSc, vat, lineTotal);
    }

    public static HeaderPricingResult ComputeHeader(
        IReadOnlyCollection<LinePricingResult> lines, IRoundingConfig rc)
    {
        decimal subtotal = Money.Round(lines.Sum(l => l.LineSubtotal), rc, RoundingKeys.TicketSubtotal);
        decimal lineDiscTotal = Money.Round(lines.Sum(l => l.LineDiscountAmount), rc, RoundingKeys.TicketDiscount);
        decimal ticketDiscTotal = Money.Round(lines.Sum(l => l.TicketDiscountAmount), rc, RoundingKeys.TicketDiscount);
        decimal discountAmount = lineDiscTotal + ticketDiscTotal;
        decimal serviceCharge = Money.Round(lines.Sum(l => l.ServiceChargeAmount), rc, RoundingKeys.TicketSc);
        decimal vat = Money.Round(lines.Sum(l => l.VatAmount), rc, RoundingKeys.TicketVat);
        decimal total = Money.Round(lines.Sum(l => l.LineTotal), rc, RoundingKeys.TicketTotal);

        decimal roundingAdjustment = Money.Round(
            total - (subtotal - discountAmount + serviceCharge + vat),
            rc, RoundingKeys.TicketAdjust);

        return new HeaderPricingResult(
            subtotal, lineDiscTotal, ticketDiscTotal, discountAmount,
            serviceCharge, vat, total, roundingAdjustment);
    }
}
