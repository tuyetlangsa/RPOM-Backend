using Rpom.Domain.Common;

namespace Rpom.Domain.Restaurant;

/// <summary>
///     Quầy phục vụ — top of spatial hierarchy (Counter → Area → Table).
///     Login context: every operational user picks 1 Counter at login, scoping
///     all subsequent screens. Also scopes CashDrawerSession and AI EOD Summary.
/// </summary>
public class Counter : Entity
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Note { get; set; }
    public short DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Area> Areas { get; set; } = new List<Area>();
}
