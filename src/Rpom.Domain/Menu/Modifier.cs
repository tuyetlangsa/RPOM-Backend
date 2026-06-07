using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

/// <summary>
///     M:N junction ChoiceCategory ↔ Item (modifier IS Item — F2 pattern).
///     Each row = a modifier option (an Item) within a ChoiceCategory, with its
///     own extra price and per-option constraints. Composite PK (ChoiceCategoryId, ItemId).
/// </summary>
public class Modifier : Entity
{
    public int ChoiceCategoryId { get; set; }

    /// <summary>The Item playing the role of modifier option in this group.</summary>
    public int ItemId { get; set; }

    /// <summary>Fixed amount added to SET_MENU price when this modifier is selected (× quantity at order time).</summary>
    public decimal ExtraPrice { get; set; }

    /// <summary>Per-option lower bound; 0 = optional.</summary>
    public int MinPerModifier { get; set; }

    /// <summary>Per-option upper bound; e.g. 1 = at most 1, 5 = up to 5 times.</summary>
    public int MaxPerModifier { get; set; } = 1;

    public short DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ChoiceCategory ChoiceCategory { get; set; } = null!;
    public virtual Item Item { get; set; } = null!;
}
