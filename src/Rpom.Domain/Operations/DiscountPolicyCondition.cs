using Rpom.Domain.Common;
using Rpom.Domain.Menu;
using Rpom.Domain.Restaurant;

namespace Rpom.Domain.Operations;

/// <summary>
///     Condition rows for a DiscountPolicy. Field usage depends on parent.DiscountType:
///     TICKET_THRESHOLD → ThresholdAmount set, ItemId/QuantityThreshold NULL.
///     QUANTITY_ITEM → ItemId + QuantityThreshold set, ThresholdAmount NULL.
///     AreaId optional regardless of type (scopes condition to that Area).
///     Multiple rows per policy support tiered discounts (highest matching threshold wins).
/// </summary>
public class DiscountPolicyCondition : Entity
{
    public int Id { get; set; }

    /// <summary>Parent policy — cascade delete.</summary>
    public int DiscountPolicyId { get; set; }

    /// <summary>TICKET_THRESHOLD: ticket subtotal must be ≥ this. NULL for QUANTITY_ITEM.</summary>
    public decimal? ThresholdAmount { get; set; }

    /// <summary>QUANTITY_ITEM: which Item triggers the discount. NULL for TICKET_THRESHOLD.</summary>
    public int? ItemId { get; set; }

    /// <summary>QUANTITY_ITEM: customer must buy ≥ this many of ItemId. NULL for TICKET_THRESHOLD.</summary>
    public decimal? QuantityThreshold { get; set; }

    /// <summary>Optional scope: condition only applies when ordering from this Area. NULL = all Areas.</summary>
    public int? AreaId { get; set; }

    /// <summary>PERCENT | FIXED (see <see cref="DiscountApplyType" />).</summary>
    public string ApplyType { get; set; } = null!;

    /// <summary>PERCENT: 0-100 percentage. FIXED: VND amount.</summary>
    public decimal DiscountValue { get; set; }

    public short DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual DiscountPolicy DiscountPolicy { get; set; } = null!;
    public virtual Item? Item { get; set; }
    public virtual Area? Area { get; set; }
}
