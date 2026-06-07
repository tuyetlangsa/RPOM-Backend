namespace Rpom.Application.Abstraction.Pricing;

/// <summary>
///     Resolves the raw price of menu items for an area at a point in time, using the price
///     table in effect and most-specific-variant-wins (pricing spec §13). Shared by GetMenu
///     (whole menu) and the cashier write flow (single item at add-to-cart) so both agree on price.
/// </summary>
public interface IMenuPriceResolver
{
    /// <summary>
    ///     Resolve prices for <paramref name="itemIds" /> in <paramref name="areaId" /> at
    ///     <paramref name="at" />. Returns the active price table (null when none is in effect)
    ///     and the winning entry per item (items without a price are absent from the dictionary).
    /// </summary>
    Task<MenuPriceResolution> ResolveAsync(
        int areaId, DateTime at, IReadOnlyCollection<int> itemIds, CancellationToken ct);
}

public sealed record MenuPriceResolution(
    int? PriceTableId,
    string? PriceTableName,
    IReadOnlyDictionary<int, ResolvedPrice> Prices);

public readonly record struct ResolvedPrice(decimal Price, bool IsVatIncluded);
