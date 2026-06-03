using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

/// <summary>
/// Reusable group of modifier options (e.g. "Loại nước", "Mức độ cay", "Topping").
/// Attached to SET_MENU items via SetMenuDetail (DetailType = CHOICE_CATEGORY).
/// Modifier options are Items themselves, linked via the Modifier junction.
/// </summary>
public class ChoiceCategory : Entity
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Note { get; set; }

    /// <summary>Minimum number of options customer must select from this group.</summary>
    public short MinChoice { get; set; } = 1;

    /// <summary>Maximum; NULL = unlimited.</summary>
    public short? MaxChoice { get; set; }
    public short DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Modifier> Modifiers { get; set; } = new List<Modifier>();
}
