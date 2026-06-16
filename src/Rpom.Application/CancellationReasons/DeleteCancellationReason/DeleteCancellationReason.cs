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
using Rpom.Domain.Sales;

namespace Rpom.Application.CancellationReasons.DeleteCancellationReason;

/// <summary>
///     BR-CR2: historically referenced cancellation reasons cannot be hard-deleted.
///     Use Update with IsActive=false to retire instead.
/// </summary>
public static class DeleteCancellationReason
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
            CancellationReason? entity = await dbContext.CancellationReasons.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null)
            {
                return Result.Failure(CancellationReasonErrors.NotFound);
            }

            // Reference check — OrderItem and Ticket have soft FK to CancellationReason.
            int id = request.Id;
            bool inUse =
                await dbContext.OrderItems.AnyAsync(x => x.CancellationReasonId == id, ct)
                || await dbContext.Tickets.AnyAsync(x => x.CancellationReasonId == id, ct);

            if (inUse)
            {
                return Result.Failure(CancellationReasonErrors.InUse);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;
            string snapshotCode = entity.Code;
            string snapshotName = entity.Name;

            dbContext.CancellationReasons.Remove(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(CancellationReason),
                EntityId = id,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"CancellationReason deleted: {snapshotCode} — {snapshotName}"
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Config, $"CancellationReason.Delete(id={id})", ct);
            return Result.Success();
        }
    }
}
