using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

/// <summary>
/// Associative Item × PriceVariant → Price. UNIQUE (PriceVariantId, ItemId).
/// UI grid pivot in PriceTable detail screen uses these as cells.
/// </summary>
public class PriceEntry : Entity
{
    public int Id { get; set; }

    /// <summary>FK to PriceVariant (NOT PriceTable). Variant has conditions; entry has price.</summary>
    public int PriceVariantId { get; set; }
    public int ItemId { get; set; }

    /// <summary>Price for this Item under this PriceVariant.</summary>
    public decimal Price { get; set; }

    /// <summary>true → Price already includes VAT; false → VAT added on top.</summary>
    public bool IsVatIncluded { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual PriceVariant PriceVariant { get; set; } = null!;
    public virtual Item Item { get; set; } = null!;
}
