using Rpom.Domain.Common;

namespace Rpom.Domain.Restaurant;

/// <summary>
///     Advisory-but-enforced operation lock on a Table: at most one staff may operate a
///     table (open ticket, edit cart, send order) at a time. One row per locked table
///     (TableId is the PK). A lock is considered stale/free once
///     <see cref="LastHeartbeatAt" /> + TTL has passed, so a crashed terminal never holds
///     a table forever. The cashier app heartbeats periodically while the table screen is open.
/// </summary>
public class TableLock : Entity
{
    /// <summary>PK + FK to Table — one lock per table.</summary>
    public int TableId { get; set; }

    public int StaffAccountId { get; set; }

    /// <summary>Denormalised holder name for "đang thao tác bởi X" display on the floor plan.</summary>
    public string StaffName { get; set; } = null!;

    public DateTime AcquiredAt { get; set; }

    /// <summary>Refreshed on every acquire/heartbeat and on every guarded write.</summary>
    public DateTime LastHeartbeatAt { get; set; }

    public virtual Table Table { get; set; } = null!;
}
