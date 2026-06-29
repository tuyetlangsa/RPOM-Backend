using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Domain.Reservation;

/// <summary>
///     Junction: one booked table of a reservation (many-to-many). Source of truth for
///     BR-R1 overlap, UC-R5 projection, and the floor-plan "đã đặt" badge. PK = (ReservationId, TableId).
/// </summary>
public class ReservationTable : Entity
{
    public long ReservationId { get; set; }
    public int TableId { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Reservation Reservation { get; set; } = null!;
    public virtual Table Table { get; set; } = null!;
}
