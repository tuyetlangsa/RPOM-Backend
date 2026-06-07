using Rpom.Domain.Access;
using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

/// <summary>
///     1 round of sending to kitchen. A ticket has multiple Orders (multiple rounds).
///     On DRAFT → SENT: CartItem rows become OrderItem rows; cart is cleared.
///     Class name kept as "Order" — table name will be quoted ("orders") in EF config.
/// </summary>
public class Order : Entity
{
    public long Id { get; set; }
    public long TicketId { get; set; }

    /// <summary>Sequential within ticket: 1 (round 1), 2 (round 2), ... for "Đợt 1", "Đợt 2" display.</summary>
    public short OrderNumber { get; set; }

    /// <summary>DRAFT | SENT | CANCELLED (see <see cref="OrderStatus" />).</summary>
    public string Status { get; set; } = OrderStatus.Draft;

    /// <summary>Set when transitioning DRAFT → SENT; NULL otherwise.</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>Waiter who created this round. NULL for QR self-order (Guest).</summary>
    public int? CreatedByStaffId { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Poll cursor — kitchen pending list, waiter cart sync.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency — waiter add cart vs. send-to-kitchen race.</summary>
    public int Version { get; set; }

    public virtual Ticket Ticket { get; set; } = null!;
    public virtual StaffAccount? CreatedByStaff { get; set; }
    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
