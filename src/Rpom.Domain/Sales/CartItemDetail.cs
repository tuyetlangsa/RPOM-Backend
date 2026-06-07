using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Domain.Sales;

/// <summary>
///     Sub-detail capturing customer picks within a SET_MENU CartItem
///     (main components kept + modifier selections).
/// </summary>
public class CartItemDetail : Entity
{
    public int Id { get; set; }
    public long CartItemId { get; set; }

    /// <summary>Which ChoiceCategory this option satisfies (MODIFIER). NULL for MAIN_COMPONENT.</summary>
    public int? ChoiceCategoryId { get; set; }

    /// <summary>The Item picked (modifier or component — both are Items).</summary>
    public int ItemId { get; set; }

    /// <summary>Snapshot.</summary>
    public string ItemName { get; set; } = null!;

    /// <summary>MAIN_COMPONENT | MODIFIER (see <see cref="ComponentType" />).</summary>
    public string ComponentType { get; set; } = null!;

    public decimal Quantity { get; set; } = 1;

    /// <summary>Snapshot from Modifier.ExtraPrice at order time. 0 for MAIN_COMPONENT (per BR-35).</summary>
    public decimal ExtraPrice { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual CartItem CartItem { get; set; } = null!;
    public virtual ChoiceCategory? ChoiceCategory { get; set; }
    public virtual Item Item { get; set; } = null!;
}
