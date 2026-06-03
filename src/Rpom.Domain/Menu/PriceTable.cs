using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

/// <summary>
/// Top-level pricing strategy container. Groups 1..N PriceVariants which carry
/// the actual conditional pricing logic. PriceTable provides strategy-wide
/// validity window and on/off switch only.
/// </summary>
public class PriceTable : Entity
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>Strategy-level validity start. Variants inherit when their own BeginDate IS NULL.</summary>
    public DateOnly? BeginDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>Disable entire strategy = disable all variants under it.</summary>
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<PriceVariant> PriceVariants { get; set; } = new List<PriceVariant>();
}
