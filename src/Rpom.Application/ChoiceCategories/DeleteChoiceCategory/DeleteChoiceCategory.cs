using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.ChoiceCategories.DeleteChoiceCategory;

/// <summary>
/// Hard-delete a ChoiceCategory (its Modifiers cascade away). Refuses if any
/// SetMenuDetail still references it.
/// </summary>
public static class DeleteChoiceCategory
{
    public sealed record Command(int Id) : ICommand;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() { RuleFor(x => x.Id).GreaterThan(0); }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var entity = await db.ChoiceCategories.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
            if (entity is null) return Result.Failure(ChoiceCategoryErrors.NotFound);

            var inUse = await db.SetMenuDetails.AnyAsync(d => d.ChoiceCategoryId == request.Id, ct);
            if (inUse) return Result.Failure(ChoiceCategoryErrors.InUse);

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;
            var snapshotName = entity.Name;

            db.ChoiceCategories.Remove(entity);

            var staff = await db.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(ChoiceCategory),
                EntityId = request.Id,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"ChoiceCategory deleted: {snapshotName}",
            });

            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"ChoiceCategory.Delete(id={request.Id})", ct);
            return Result.Success();
        }
    }
}
