using Rpom.Domain.Common;

namespace Rpom.Domain.Restaurant;

/// <summary>
/// Bàn ăn — physical table in a dining area. Status lifecycle is driven by
/// Ticket lifecycle (Area E): AVAILABLE ↔ OCCUPIED. RESERVED state is NOT
/// stored — computed at Floor Plan render-time by joining with Reservation.
/// </summary>
public class Table : Entity
{
    public int Id { get; set; }
    public int AreaId { get; set; }

    /// <summary>Owner-defined business code, unique within Area (e.g. "T01", "VIP1").</summary>
    public string Code { get; set; } = null!;

    /// <summary>Default seating capacity.</summary>
    public int SeatCount { get; set; } = 1;
    public string? Description { get; set; }

    /// <summary>AVAILABLE | OCCUPIED (see <see cref="TableStatus"/>).</summary>
    public string Status { get; set; } = TableStatus.Available;

    /// <summary>Soft delete — historical tickets keep referencing the row.</summary>
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// POLL CURSOR for Floor Plan UI: queries WHERE AreaId IN (...) AND UpdatedAt > @since.
    /// Updated in same transaction as the Ticket state change that flips Status.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    public virtual Area Area { get; set; } = null!;
}
