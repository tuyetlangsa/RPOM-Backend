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
using Rpom.Domain.Menu;

namespace Rpom.Application.Categories.DeleteCategory;

public static class DeleteCategory
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
            Category? entity = await dbContext.Categories.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null)
            {
                return Result.Failure(CategoryErrors.NotFound);
            }

            bool hasItems = await dbContext.ItemCategories.AnyAsync(ic => ic.CategoryId == request.Id, ct);
            if (hasItems)
            {
                return Result.Failure(CategoryErrors.InUseByItems);
            }

            bool hasChildren = await dbContext.Categories.AnyAsync(c => c.ParentId == request.Id, ct);
            if (hasChildren)
            {
                return Result.Failure(CategoryErrors.InUseByChildren);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;
            string snapshotName = entity.Name;
            string snapshotCode = entity.Code;

            dbContext.Categories.Remove(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Category),
                EntityId = request.Id,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Category deleted: {snapshotCode} — {snapshotName}"
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"Category.Delete(id={request.Id})", ct);
            return Result.Success();
        }
    }
}
