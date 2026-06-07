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

namespace Rpom.Application.PriceTables.DeletePriceTable;

public static class DeletePriceTable
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
            PriceTable? entity = await dbContext.PriceTables.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null)
            {
                return Result.Failure(PriceTableErrors.NotFound);
            }

            bool hasVariants = await dbContext.PriceVariants.AnyAsync(v => v.PriceTableId == request.Id, ct);
            if (hasVariants)
            {
                return Result.Failure(PriceTableErrors.HasVariants);
            }

            DateTime now = clock.UtcNow;
            int staffId = currentStaff.StaffAccountId;
            string snapshotCode = entity.Code;
            string snapshotName = entity.Name;

            dbContext.PriceTables.Remove(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(PriceTable),
                EntityId = request.Id,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"PriceTable deleted: {snapshotCode} — {snapshotName}"
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"PriceTable.Delete(id={request.Id})", ct);
            return Result.Success();
        }
    }
}
