using Rpom.Application.Abstraction.Pricing;

namespace Rpom.Application.Cashier.GetMenu;

/// <summary>Pure menu price helpers (pricing spec §3.4 + §13 most-specific-wins).</summary>
public static class MenuPricing
{
    public readonly record struct VariantRank(int VariantId, int Spec, DateTime CreatedAt);

    /// <summary>(BasePrice pre-VAT pre-SC, DisplayPrice all-in VAT no SC).</summary>
    public static (decimal BasePrice, decimal DisplayPrice) ComputePrices(
        decimal price, bool isVatIncluded, decimal vatPercent, IRoundingConfig rc)
    {
        var basePrice = isVatIncluded
            ? Money.Round(price / (1 + vatPercent / 100m), rc, RoundingKeys.PriceDetail)
            : price;
        var displayPrice = isVatIncluded
            ? price
            : Money.Round(price * (1 + vatPercent / 100m), rc, RoundingKeys.MenuDisplay);
        return (basePrice, displayPrice);
    }

    /// <summary>Winning variant id: highest Spec, tie broken by newest CreatedAt.</summary>
    public static int PickWinner(IReadOnlyCollection<VariantRank> candidates) =>
        candidates
            .OrderByDescending(c => c.Spec)
            .ThenByDescending(c => c.CreatedAt)
            .First().VariantId;
}
