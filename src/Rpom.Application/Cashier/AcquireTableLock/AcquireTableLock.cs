using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Cashier.AcquireTableLock;

/// <summary>
///     Acquire-or-heartbeat the operation lock on a table. Idempotent for the holder:
///     a free / mine / stale lock is (re)claimed and its heartbeat refreshed; a live lock
///     held by another staff is rejected. A genuine (re)acquisition bumps FLOOR_PLAN so other
///     terminals disable the table; a pure heartbeat does not.
/// </summary>
public static class AcquireTableLock
{
    public sealed record Command(int TableId) : ICommand<Response>;

    public sealed record Response(
        int TableId,
        int StaffAccountId,
        string StaffName,
        DateTime AcquiredAt,
        DateTime ExpiresAt);

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var table = await db.Tables
                .Where(t => t.Id == request.TableId && t.IsActive)
                .Select(t => new { t.Id })
                .FirstOrDefaultAsync(ct);
            if (table is null)
            {
                return Result.Failure<Response>(TableErrors.NotFound);
            }

            DateTime now = clock.UtcNow;
            DateTime cutoff = now.AddSeconds(-ITableOperationGuard.TtlSeconds);
            int staffId = currentStaff.StaffAccountId;

            var staff = await db.StaffAccounts
                .Where(s => s.Id == staffId)
                .Select(s => new { s.FullName })
                .FirstAsync(ct);

            TableLock? lockRow = await db.TableLocks.FirstOrDefaultAsync(l => l.TableId == request.TableId, ct);

            bool newAcquisition;
            if (lockRow is null)
            {
                lockRow = new TableLock
                {
                    TableId = request.TableId,
                    StaffAccountId = staffId,
                    StaffName = staff.FullName,
                    AcquiredAt = now,
                    LastHeartbeatAt = now
                };
                db.TableLocks.Add(lockRow);
                newAcquisition = true;
            }
            else if (lockRow.StaffAccountId == staffId)
            {
                // Mine — pure heartbeat.
                lockRow.LastHeartbeatAt = now;
                lockRow.StaffName = staff.FullName;
                newAcquisition = false;
            }
            else if (lockRow.LastHeartbeatAt < cutoff)
            {
                // Held by someone else but stale — take over.
                lockRow.StaffAccountId = staffId;
                lockRow.StaffName = staff.FullName;
                lockRow.AcquiredAt = now;
                lockRow.LastHeartbeatAt = now;
                newAcquisition = true;
            }
            else
            {
                return Result.Failure<Response>(TableLockErrors.HeldByOther(lockRow.StaffName));
            }

            await db.SaveChangesAsync(ct);
            if (newAcquisition)
            {
                await versionService.BumpAsync(VersionScopes.FloorPlan,
                    $"TableLock.Acquire(tableId={request.TableId})", ct);
            }

            return Result.Success(new Response(
                lockRow.TableId, lockRow.StaffAccountId, lockRow.StaffName,
                lockRow.AcquiredAt, lockRow.LastHeartbeatAt.AddSeconds(ITableOperationGuard.TtlSeconds)));
        }
    }
}
