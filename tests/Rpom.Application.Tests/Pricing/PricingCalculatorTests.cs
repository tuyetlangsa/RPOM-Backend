using FluentAssertions;
using NSubstitute;
using Rpom.Application.Abstraction.Pricing;

namespace Rpom.Application.Tests.Pricing;

public class PricingCalculatorTests
{
    // Rounding config matching the 14 seeded defaults.
    private static IRoundingConfig DefaultRc()
    {
        var rc = Substitute.For<IRoundingConfig>();
        foreach (var kv in RoundingKeys.Defaults)
            rc.GetDigits(kv.Key).Returns(kv.Value);
        return rc;
    }

    private static LinePricingInput Line(
        decimal qty, decimal unit, decimal vat,
        decimal sc = 0, decimal scVat = 0,
        decimal lineDisc = 0, decimal ticketDisc = 0, decimal choice = 0) =>
        new(qty, unit, choice, vat, sc, scVat, lineDisc, ticketDisc);

    [Fact] // TC-01: 1 line, VAT 10%, no SC, no discount
    public void TC01_SingleLine_Vat10_NoScNoDiscount()
    {
        var rc = DefaultRc();
        var r = PricingCalculator.ComputeLine(Line(2, 50000m, 10m), rc);

        r.LineSubtotal.Should().Be(100000m);
        r.ServiceChargeAmount.Should().Be(0m);
        r.VatItemAmount.Should().Be(10000m);
        r.VatAmount.Should().Be(10000m);
        r.LineTotal.Should().Be(110000m); // = qty × displayPrice (55000)
    }

    [Fact] // TC-02: VAT 8%, cross-check qty×displayPrice ≈ lineTotal (±1 VND)
    public void TC02_SingleLine_Vat8()
    {
        var rc = DefaultRc();
        var r = PricingCalculator.ComputeLine(Line(1, 80000m, 8m), rc);

        r.LineSubtotal.Should().Be(80000m);
        r.VatItemAmount.Should().Be(6400m);
        r.LineTotal.Should().Be(86400m);
        var displayLineTotal = 1 * Math.Round(80000m * 1.08m, 0, MidpointRounding.AwayFromZero);
        Math.Abs(r.LineTotal - displayLineTotal).Should().BeLessThanOrEqualTo(1m);
    }

    [Fact] // TC-03: multi-line SC 10% VAT 10%, header sums + RoundingAdjustment = 0
    public void TC03_MultiLine_Sc10_Vat10_HeaderRollup()
    {
        var rc = DefaultRc();
        var l1 = PricingCalculator.ComputeLine(Line(2, 50000m, 10m, sc: 10m, scVat: 10m), rc);
        var l2 = PricingCalculator.ComputeLine(Line(1, 80000m, 8m, sc: 10m, scVat: 10m), rc);
        var h = PricingCalculator.ComputeHeader(new[] { l1, l2 }, rc);

        h.Subtotal.Should().Be(180000m);
        h.DiscountAmount.Should().Be(0m);
        h.ServiceChargeAmount.Should().Be(18000m);
        h.VatAmount.Should().Be(18200m);
        h.TotalAmount.Should().Be(216200m);
        h.RoundingAdjustment.Should().Be(0m);
    }

    [Fact] // TC-04: line 10% + ticket 5% on same line → keep line, ticket zero
    public void TC04_OneLevelDiscount_LineWins()
    {
        var rc = DefaultRc();
        var r = PricingCalculator.ComputeLine(Line(1, 100000m, 10m, lineDisc: 10m, ticketDisc: 5m), rc);

        r.LineDiscountAmount.Should().Be(10000m);
        r.TicketDiscountAmount.Should().Be(0m);
        r.TotalDiscountAmount.Should().Be(10000m);
    }

    [Fact] // TC-05: line 5% + ticket 10% → keep ticket, line zero
    public void TC05_OneLevelDiscount_TicketWins()
    {
        var rc = DefaultRc();
        var r = PricingCalculator.ComputeLine(Line(1, 100000m, 10m, lineDisc: 5m, ticketDisc: 10m), rc);

        r.LineDiscountAmount.Should().Be(0m);
        r.TicketDiscountAmount.Should().Be(10000m);
        r.TotalDiscountAmount.Should().Be(10000m);
    }

    [Fact] // TC-06: refund (negative qty) → negative amounts allowed
    public void TC06_Refund_NegativeQuantity()
    {
        var rc = DefaultRc();
        var r = PricingCalculator.ComputeLine(Line(-1, 50000m, 10m), rc);

        r.LineSubtotal.Should().Be(-50000m);
        r.LineTotal.Should().Be(-55000m);
    }

    [Fact] // TC-07: cart line invariant — LineTotal == subtotal + SC + VAT (no discount)
    public void TC07_CartLineInvariant()
    {
        var rc = DefaultRc();
        var r = PricingCalculator.ComputeLine(Line(3, 30000m, 10m, sc: 5m, scVat: 10m), rc);

        (r.LineSubtotal - r.TotalDiscountAmount + r.ServiceChargeAmount + r.VatAmount)
            .Should().Be(r.LineTotal);
    }

    [Fact] // TC-09: discount applied changes VatItem base (sau discount)
    public void TC09_VatComputedAfterDiscount()
    {
        var rc = DefaultRc();
        var r = PricingCalculator.ComputeLine(Line(1, 100000m, 10m, ticketDisc: 10m), rc);

        // VAT base = (100000 − 10000) × 10% = 9000
        r.VatItemAmount.Should().Be(9000m);
    }

    [Fact] // TC-10: RoundingAdjustment captures rollup vs raw difference
    public void TC10_RoundingAdjustment_NonZero_WhenRawDiffers()
    {
        var rc = DefaultRc();
        // Construct lines whose 0-dp header totals differ from the raw arithmetic sum.
        var l = PricingCalculator.ComputeLine(Line(3, 33333m, 10m, sc: 10m, scVat: 10m), rc);
        var h = PricingCalculator.ComputeHeader(new[] { l }, rc);

        var raw = h.Subtotal - h.DiscountAmount + h.ServiceChargeAmount + h.VatAmount;
        h.RoundingAdjustment.Should().Be(Math.Round(h.TotalAmount - raw, 2, MidpointRounding.AwayFromZero));
    }
}
