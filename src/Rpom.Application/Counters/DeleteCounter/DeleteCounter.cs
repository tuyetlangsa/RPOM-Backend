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
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Counters.DeleteCounter;

/// <summary>
///     Hard-delete a Counter. Refuses if any Area still references it — caller
///     must move/delete Areas first. For temporary disable use Update with
///     IsActive=false instead.
/// </summary>
public static class DeleteCounter
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
            Counter? entity = await dbContext.Counters.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null)
            {
                return Result.Failure(CounterErrors.NotFound);
            }

            bool hasAreas = await dbContext.Areas.AnyAsync(x => x.CounterId == request.Id, ct);
            if (hasAreas)
            {
                return Result.Failure(CounterErrors.InUse);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;
            string snapshotName = entity.Name;

            dbContext.Counters.Remove(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Counter),
                EntityId = request.Id,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Counter deleted: {snapshotName}"
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Counter.Delete(id={request.Id})", ct);
            return Result.Success();
        }
    }
}
