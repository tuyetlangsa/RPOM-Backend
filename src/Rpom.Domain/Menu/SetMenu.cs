using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

/// <summary>
/// 1:1 specialization of Item for SET_MENU aspect. PK = FK = ItemId.
/// Existence of row = marker "this Item is a SET_MENU" (no flag/enum on Item).
/// Components + choice categories captured in SetMenuDetail.
/// </summary>
public class SetMenu : Entity
{
    /// <summary>PK = FK to Item.Id. 1:1 specialization, no separate Id.</summary>
    public int ItemId { get; set; }

    /// <summary>SET_MENU-specific description; complements Item.Description.</summary>
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Item Item { get; set; } = null!;
    public virtual ICollection<SetMenuDetail> Details { get; set; } = new List<SetMenuDetail>();
}
