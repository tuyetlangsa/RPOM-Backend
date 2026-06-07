using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Domain.Sales;

/// <summary>
///     Sub-detail of OrderItem for SET_MENU breakdown. Mirrors CartItemDetail but
///     for SENT state (immutable once parent sent). Created from CartItemDetail on
///     Order DRAFT → SENT.
/// </summary>
public class OrderItemDetail : Entity
{
    public int Id { get; set; }
    public long OrderItemId { get; set; }

    /// <summary>NULL for MAIN_COMPONENT.</summary>
    public int? ChoiceCategoryId { get; set; }

    public int ItemId { get; set; }
    public string ItemName { get; set; } = null!;

    /// <summary>MAIN_COMPONENT | MODIFIER (see <see cref="ComponentType" />).</summary>
    public string ComponentType { get; set; } = null!;

    public decimal Quantity { get; set; } = 1;
    public decimal ExtraPrice { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual OrderItem OrderItem { get; set; } = null!;
    public virtual ChoiceCategory? ChoiceCategory { get; set; }
    public virtual Item Item { get; set; } = null!;
}
