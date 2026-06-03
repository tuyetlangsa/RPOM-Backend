using Rpom.Domain.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;
using Rpom.Domain.Operations;

namespace Rpom.Domain.Sales;

/// <summary>
/// Sent line item with kitchen lifecycle. KitchenStationId is a SNAPSHOT at
/// SEND time — if Item.KitchenStationId changes later, in-flight rows keep
/// the original. v1: SET_MENU components share parent OrderItem status.
/// <para>
/// Damaged-dish refund pattern: when a DONE dish needs to be refunded
/// (broken, quality issue), a NEW OrderItem row is created with NEGATIVE
/// Quantity, OriginalOrderItemId pointing back to the original DONE row.
/// The refund row runs through its own PENDING → ... → DONE lifecycle
/// (re-cook a replacement, or just settle with negative-money line).
/// CANCELLED status is reserved for out-of-stock (PENDING-only).
/// </para>
/// </summary>
public class OrderItem : Entity
{
    public long Id { get; set; }

    /// <summary>Parent order — must be Status=SENT.</summary>
    public long OrderId { get; set; }

    /// <summary>DENORM from Order.TicketId — fast "all dishes of ticket X" for KDS/POS.</summary>
    public long TicketId { get; set; }

    public int ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public int UomId { get; set; }

    /// <summary>
    /// Quantity. May be NEGATIVE for refund-line rows (damaged-dish pattern) —
    /// in that case OriginalOrderItemId points back to the original positive row.
    /// </summary>
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>
    /// Snapshot of Item.KitchenStationId at SEND time. NULL for SET_MENU containers
    /// or non-kitchen items. Does NOT auto-update if Item.KitchenStationId changes.
    /// </summary>
    public int? KitchenStationId { get; set; }

    /// <summary>PENDING | PROCESSING | READY | DONE | CANCELLED (see <see cref="OrderItemStatus"/>).</summary>
    public string Status { get; set; } = OrderItemStatus.Pending;

    /// <summary>When CartItem → OrderItem transition happened.</summary>
    public DateTime SentAt { get; set; }
    public DateTime? StartCookAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? DoneAt { get; set; }

    public DateTime? CancelledAt { get; set; }
    public int? CancellationReasonId { get; set; }
    public string? CancellationNote { get; set; }

    /// <summary>Who initiated cancel (waiter); manager approval audit-logged separately.</summary>
    public int? CancelledByStaffId { get; set; }

    /// <summary>
    /// Self-ref to the original positive-Quantity OrderItem this row refunds/replaces.
    /// NULL for normal (positive Quantity) rows. Required when Quantity &lt; 0.
    /// App-enforced — DB cannot CHECK cross-row logic.
    /// </summary>
    public long? OriginalOrderItemId { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Poll cursor — KDS status board realtime refresh.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency — KDS chef vs. waiter cancel race.</summary>
    public int Version { get; set; }

    public virtual Order Order { get; set; } = null!;
    public virtual Ticket Ticket { get; set; } = null!;
    public virtual Item Item { get; set; } = null!;
    public virtual Uom Uom { get; set; } = null!;
    public virtual KitchenStation? KitchenStation { get; set; }
    public virtual CancellationReason? CancellationReason { get; set; }
    public virtual StaffAccount? CancelledByStaff { get; set; }
    public virtual OrderItem? OriginalOrderItem { get; set; }
    public virtual ICollection<OrderItem> RefundLines { get; set; } = new List<OrderItem>();
    public virtual ICollection<OrderItemDetail> Details { get; set; } = new List<OrderItemDetail>();
}
