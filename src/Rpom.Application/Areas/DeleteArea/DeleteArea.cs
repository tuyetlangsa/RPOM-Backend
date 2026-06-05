using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Areas.DeleteArea;

public static class DeleteArea
{
    public sealed record Command(int Id) : ICommand;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() { RuleFor(x => x.Id).GreaterThan(0); }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock) : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var entity = await dbContext.Areas.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null) return Result.Failure(AreaErrors.NotFound);

            var hasTables = await dbContext.Tables.AnyAsync(x => x.AreaId == request.Id, ct);
            if (hasTables) return Result.Failure(AreaErrors.InUse);

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;
            var snapshotName = entity.Name;

            dbContext.Areas.Remove(entity);

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Area),
                EntityId = request.Id,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Area deleted: {snapshotName}",
            });

            await dbContext.SaveChangesAsync(ct);
            return Result.Success();
        }
    }
}
