using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

/// <summary>
///     Pre-defined cancellation reasons. Referenced by OrderItem (dish cancel),
///     Ticket (ticket cancel), Reservation (booking cancel — Area F).
///     "OTHER" is the catch-all with optional note.
/// </summary>
public class CancellationReason : Entity
{
    public int Id { get; set; }

    /// <summary>CUS_CHANGE_MIND | OUT_OF_STOCK | WRONG_DISH | FOREIGN_OBJECT | QUALITY | OTHER.</summary>
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;
    public short DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
