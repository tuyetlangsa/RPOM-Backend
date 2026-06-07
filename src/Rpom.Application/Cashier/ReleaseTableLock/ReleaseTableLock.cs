using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Cashier.ReleaseTableLock;

/// <summary>
///     Release my operation lock on a table (when leaving the table screen). No-op if the
///     lock is absent or held by someone else. Bumps FLOOR_PLAN when a row was removed so
///     other terminals re-enable the table.
/// </summary>
public static class ReleaseTableLock
{
    public sealed record Command(int TableId) : ICommand;

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IVersionService versionService) : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            int staffId = currentStaff.StaffAccountId;
            TableLock? lockRow = await db.TableLocks
                .FirstOrDefaultAsync(l => l.TableId == request.TableId && l.StaffAccountId == staffId, ct);

            if (lockRow is null)
            {
                return Result.Success(); // nothing of mine to release
            }

            db.TableLocks.Remove(lockRow);
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan,
                $"TableLock.Release(tableId={request.TableId})", ct);

            return Result.Success();
        }
    }
}
