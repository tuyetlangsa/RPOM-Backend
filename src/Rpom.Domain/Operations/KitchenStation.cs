using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Domain.Operations;

/// <summary>
/// Sub-kitchen (Bếp con) where specific Items are prepared. 1 station can have
/// 0..N Printers (RPOM separates printer + kitchen, unlike F2's conflation).
/// </summary>
public class KitchenStation : Entity
{
    public int Id { get; set; }

    /// <summary>Owner-defined: BN, BL, BAR.</summary>
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public short DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();
    public virtual ICollection<Printer> Printers { get; set; } = new List<Printer>();
}
