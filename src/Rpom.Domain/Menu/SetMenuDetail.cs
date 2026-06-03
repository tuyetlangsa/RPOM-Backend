using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

/// <summary>
/// Unified detail rows attached to a SET_MENU. Discriminated by DetailType:
/// COMPONENT (fixed/optional dish) or CHOICE_CATEGORY (modifier group).
/// DisplayOrder is unified — UI renders components + choice groups interleaved.
/// </summary>
public class SetMenuDetail : Entity
{
    public int Id { get; set; }

    /// <summary>FK to SetMenu.ItemId (NOT Item.Id) — DB enforces only SET_MENU items can have details.</summary>
    public int SetMenuItemId { get; set; }

    /// <summary>COMPONENT | CHOICE_CATEGORY (see <see cref="SetMenuDetailType"/>).</summary>
    public string DetailType { get; set; } = null!;

    /// <summary>Required when DetailType=COMPONENT; NULL when CHOICE_CATEGORY.</summary>
    public int? ComponentItemId { get; set; }

    /// <summary>Required when DetailType=CHOICE_CATEGORY; NULL when COMPONENT.</summary>
    public int? ChoiceCategoryId { get; set; }

    /// <summary>Required when COMPONENT; NULL when CHOICE_CATEGORY.</summary>
    public decimal? Quantity { get; set; }

    /// <summary>Component-only: if true, customer cannot skip/replace. NULL when CHOICE_CATEGORY.</summary>
    public bool? IsFixed { get; set; }
    public short DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual SetMenu SetMenu { get; set; } = null!;
    public virtual Item? ComponentItem { get; set; }
    public virtual ChoiceCategory? ChoiceCategory { get; set; }
}
