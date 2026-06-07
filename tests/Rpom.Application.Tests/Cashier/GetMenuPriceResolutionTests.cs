using FluentAssertions;
using NSubstitute;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Cashier.GetMenu;

namespace Rpom.Application.Tests.Cashier;

public class GetMenuPriceResolutionTests
{
    private static IRoundingConfig DefaultRc()
    {
        var rc = Substitute.For<IRoundingConfig>();
        foreach (var kv in RoundingKeys.Defaults) rc.GetDigits(kv.Key).Returns(kv.Value);
        return rc;
    }

    [Fact] // TC-M6: IsVatIncluded=true, 55k, VAT 10% → base 50000, display 55000
    public void VatIncluded_DerivesBasePreVat()
    {
        var (b, d) = MenuPricing.ComputePrices(55000m, isVatIncluded: true, vatPercent: 10m, DefaultRc());
        b.Should().Be(50000m);
        d.Should().Be(55000m);
    }

    [Fact] // TC-M7: IsVatIncluded=false, 50k, VAT 10% → base 50000, display 55000
    public void VatExcluded_DerivesDisplayWithVat()
    {
        var (b, d) = MenuPricing.ComputePrices(50000m, isVatIncluded: false, vatPercent: 10m, DefaultRc());
        b.Should().Be(50000m);
        d.Should().Be(55000m);
    }

    [Fact] // most-specific-wins: spec 1 beats spec 0; tie broken by newer CreatedAt
    public void MostSpecific_WinsThenNewest()
    {
        var candidates = new[]
        {
            new MenuPricing.VariantRank(VariantId: 1, Spec: 0, CreatedAt: new DateTime(2026, 1, 1)),
            new MenuPricing.VariantRank(VariantId: 2, Spec: 1, CreatedAt: new DateTime(2026, 1, 1)),
            new MenuPricing.VariantRank(VariantId: 3, Spec: 1, CreatedAt: new DateTime(2026, 2, 1)),
        };
        MenuPricing.PickWinner(candidates).Should().Be(3);
    }
}
