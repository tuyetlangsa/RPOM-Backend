using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Domain.Menu;

/// <summary>
///     Universal master for all restaurant items — sellable goods, raw ingredients,
///     future scopes (tools, facilities, uniforms). Hub of Areas C, D, E, H, I.
///     Item kind is driven by explicit flags (IsStockable, HasRecipe), not by
///     walking the Category tree.
/// </summary>
public class Item : Entity
{
    public int Id { get; set; }

    /// <summary>Owner-defined item code (e.g. "BOPHO01", "COCA-LON"). Unique.</summary>
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>FK to base Uom. Alternative units defined via ItemUomConversion (Area H).</summary>
    public int BaseUomId { get; set; }

    /// <summary>VAT percentage (e.g. 10.00 for 10%).</summary>
    public decimal VatPercent { get; set; }

    /// <summary>
    ///     true → has an ItemStock row and StockMovement ledger entries (Area H).
    ///     false → no stock tracking (services, on-demand-cooked dishes, assets).
    ///     Source of truth for Area H stockable logic.
    /// </summary>
    public bool IsStockable { get; set; }

    /// <summary>
    ///     true → consumption deducts materials via BomLine of this item (Area H).
    ///     false → consumption deducts THIS item directly (only when IsStockable=true).
    /// </summary>
    public bool HasRecipe { get; set; }

    /// <summary>Stock alert threshold in base UoM. Only meaningful when IsStockable=true.</summary>
    public decimal? LowStockThreshold { get; set; }

    /// <summary>
    ///     FK to KitchenStation (Area D). NULL when item is not kitchen-routed
    ///     (raw ingredients, ready-to-sell shelf items, SET_MENU containers).
    /// </summary>
    public int? KitchenStationId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Uom BaseUom { get; set; } = null!;
    public virtual KitchenStation? KitchenStation { get; set; }

    /// <summary>1:1 specialization. Existence of row marks this Item as SET_MENU.</summary>
    public virtual SetMenu? SetMenu { get; set; }

    public virtual ICollection<ItemCategory> ItemCategories { get; set; } = new List<ItemCategory>();
    public virtual ICollection<PriceEntry> PriceEntries { get; set; } = new List<PriceEntry>();

    /// <summary>Roles this Item plays as a modifier option in various ChoiceCategories.</summary>
    public virtual ICollection<Modifier> ModifierRoles { get; set; } = new List<Modifier>();
}
