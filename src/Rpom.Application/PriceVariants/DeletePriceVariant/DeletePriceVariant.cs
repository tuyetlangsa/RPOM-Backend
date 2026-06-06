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

namespace Rpom.Application.PriceVariants.DeletePriceVariant;

public static class DeletePriceVariant
{
    public sealed record Command(int Id) : ICommand;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() { RuleFor(x => x.Id).GreaterThan(0); }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var entity = await dbContext.PriceVariants.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null) return Result.Failure(PriceVariantErrors.NotFound);

            var hasEntries = await dbContext.PriceEntries.AnyAsync(e => e.PriceVariantId == request.Id, ct);
            if (hasEntries) return Result.Failure(PriceVariantErrors.HasEntries);

            var areas = await dbContext.PriceVariantAreas
                .Where(a => a.PriceVariantId == request.Id)
                .ToListAsync(ct);
            if (areas.Count > 0) dbContext.PriceVariantAreas.RemoveRange(areas);

            var now = clock.UtcNow;
            var staffId = currentStaff.StaffAccountId;
            var snapshotCode = entity.Code;
            var snapshotName = entity.Name;

            dbContext.PriceVariants.Remove(entity);

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(PriceVariant),
                EntityId = request.Id,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"PriceVariant deleted: {snapshotCode} — {snapshotName}",
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"PriceVariant.Delete(id={request.Id})", ct);
            return Result.Success();
        }
    }
}
