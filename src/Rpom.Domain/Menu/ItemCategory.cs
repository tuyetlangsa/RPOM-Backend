using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

/// <summary>
///     Junction Item ↔ Category. Composite PK (ItemId, CategoryId).
///     Replaces both Item.MainCategoryId FK + ItemSubCategory junction.
/// </summary>
public class ItemCategory : Entity
{
    public int ItemId { get; set; }
    public int CategoryId { get; set; }

    /// <summary>
    ///     Exactly 1 row per Item must have IsMain=true (primary classification);
    ///     0..N rows have IsMain=false (sub-categorizations). App-enforced.
    /// </summary>
    public bool IsMain { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Item Item { get; set; } = null!;
    public virtual Category Category { get; set; } = null!;
}
