namespace Rpom.Application.Abstraction.Inventory;

/// <summary>
///     Converts a quantity expressed in some UoM to an item's base UoM using ItemUomConversion
///     (1 [uom] = FactorToBase × baseUom). The base UoM itself has an implicit factor of 1.
/// </summary>
public interface IUomConverter
{
    /// <summary>
    ///     Convert <paramref name="qty"/> (in <paramref name="uomId"/>) to the item's base UoM.
    ///     Returns null when <paramref name="uomId"/> is neither the item's base UoM nor a
    ///     registered active <c>ItemUomConversion</c> for the item.
    /// </summary>
    Task<decimal?> ToBaseAsync(int itemId, int baseUomId, int uomId, decimal qty, CancellationToken ct);

    /// <summary>
    ///     True when <paramref name="uomId"/> is usable for the item — i.e. it is the base UoM or
    ///     has an active conversion. Used by admin validators (e.g. BOM line).
    /// </summary>
    Task<bool> IsValidUomAsync(int itemId, int baseUomId, int uomId, CancellationToken ct);
}
