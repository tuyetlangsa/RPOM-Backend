using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.Shifts.DeleteShift;

/// <summary>
///     BR-S1: historically referenced shifts cannot be hard-deleted.
///     Use Update with IsActive=false to retire instead.
/// </summary>
public static class DeleteShift
{
    public sealed record Command(int Id) : ICommand;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            Shift? entity = await dbContext.Shifts.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null)
            {
                return Result.Failure(ShiftErrors.NotFound);
            }

            // Reference check — Ticket and CashDrawerSession have FK to Shift.
            int id = request.Id;
            bool inUse =
                await dbContext.Tickets.AnyAsync(x => x.ShiftId == id, ct)
                || await dbContext.CashDrawerSessions.AnyAsync(x => x.ShiftId == id, ct);

            if (inUse)
            {
                return Result.Failure(ShiftErrors.InUse);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;
            string snapshotCode = entity.Code;
            string snapshotName = entity.Name;

            dbContext.Shifts.Remove(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Shift),
                EntityId = id,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Shift deleted: {snapshotCode} — {snapshotName}"
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Config, $"Shift.Delete(id={id})", ct);
            return Result.Success();
        }
    }
}
