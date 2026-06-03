using Rpom.Domain.Common;

namespace Rpom.Domain.Operations;

/// <summary>
/// Discount rule. Two types (see <see cref="Operations.DiscountType"/>):
/// TICKET_THRESHOLD or QUANTITY_ITEM. Each policy has 1..N condition rows.
/// BR-09: no ad-hoc cashier discount — all discounts come from a policy.
/// </summary>
public class DiscountPolicy : Entity
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>TICKET_THRESHOLD | QUANTITY_ITEM (see <see cref="Operations.DiscountType"/>).</summary>
    public string DiscountType { get; set; } = null!;

    /// <summary>true = system auto-applies when conditions match; false = cashier picks manually.</summary>
    public bool IsAutoApply { get; set; }

    /// <summary>Comma-separated 1=Mon..7=Sun; NULL = all days.</summary>
    public string? DaysOfWeek { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<DiscountPolicyCondition> Conditions { get; set; } = new List<DiscountPolicyCondition>();
}
