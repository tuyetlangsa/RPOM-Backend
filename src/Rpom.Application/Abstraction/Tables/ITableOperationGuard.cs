using Rpom.Domain.Common;

namespace Rpom.Application.Abstraction.Tables;

/// <summary>
///     Enforces the "one staff operates a table at a time" rule for cashier writes.
///     A lock is held for <see cref="DefaultTtlSeconds" /> seconds after its last heartbeat; an
///     active write refreshes the heartbeat. Implementations mutate the tracked lock row
///     (heartbeat refresh) but do NOT call SaveChanges — the caller's handler owns the
///     unit of work.
/// </summary>
public interface ITableOperationGuard
{
    /// <summary>Fallback lock lifetime (seconds) used when the config row is unset.</summary>
    const int DefaultTtlSeconds = 60;

    /// <summary>
    ///     Ensure <paramref name="staffId" /> holds a live lock on <paramref name="tableId" />,
    ///     refreshing its heartbeat. Returns failure (<c>TableLock.NotHeld</c>) when no live
    ///     lock is held by this staff.
    /// </summary>
    Task<Result> EnsureHeldAsync(int tableId, int staffId, CancellationToken ct);
}
