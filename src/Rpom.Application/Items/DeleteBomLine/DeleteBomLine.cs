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
using Rpom.Domain.Inventory;

namespace Rpom.Application.Items.DeleteBomLine;

public static class DeleteBomLine
{
    public sealed record Command(int ItemId, int Id) : ICommand;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ItemId).GreaterThan(0);
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
            BomLine? entity = await dbContext.BomLines
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.SellableItemId == request.ItemId, ct);
            if (entity is null)
            {
                return Result.Failure(BomLineErrors.NotFound);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            dbContext.BomLines.Remove(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(BomLine),
                EntityId = request.Id,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"BomLine deleted: SellableItemId={request.ItemId}, MaterialItemId={entity.MaterialItemId}"
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"BomLine.Delete(id={request.Id})", ct);
            return Result.Success();
        }
    }
}
