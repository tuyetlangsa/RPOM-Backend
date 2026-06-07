using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Tables;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Infrastructure.Tables;

internal sealed class TableOperationGuard(IDbContext db, IDateTimeProvider clock)
    : ITableOperationGuard
{
    public async Task<Result> EnsureHeldAsync(int tableId, int staffId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var cutoff = now.AddSeconds(-ITableOperationGuard.TtlSeconds);

        var lockRow = await db.TableLocks.FirstOrDefaultAsync(l => l.TableId == tableId, ct);
        if (lockRow is null
            || lockRow.StaffAccountId != staffId
            || lockRow.LastHeartbeatAt < cutoff)
        {
            return Result.Failure(TableLockErrors.NotHeld);
        }

        // An active write keeps the lock alive; persisted by the caller's SaveChanges.
        lockRow.LastHeartbeatAt = now;
        return Result.Success();
    }
}
