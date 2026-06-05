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

namespace Rpom.Application.Uoms.DeleteUom;

/// <summary>
/// BR-4: Hard-delete refused if ANY of 6 referring tables has rows pointing
/// here. Owner should use Update with IsActive=false to retire instead.
/// </summary>
public static class DeleteUom
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
            var entity = await dbContext.Uoms.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null) return Result.Failure(UomErrors.NotFound);

            // Reference check across all 6 sites (master, config, transactional).
            var id = request.Id;
            var inUse =
                await dbContext.Items.AnyAsync(x => x.BaseUomId == id, ct)
                || await dbContext.ItemUomConversions.AnyAsync(x => x.UomId == id, ct)
                || await dbContext.BomLines.AnyAsync(x => x.UomId == id, ct)
                || await dbContext.CartItems.AnyAsync(x => x.UomId == id, ct)
                || await dbContext.OrderItems.AnyAsync(x => x.UomId == id, ct)
                || await dbContext.TicketItemSums.AnyAsync(x => x.UomId == id, ct);

            if (inUse) return Result.Failure(UomErrors.InUse);

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;
            var snapshotCode = entity.Code;
            var snapshotName = entity.Name;

            dbContext.Uoms.Remove(entity);

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Uom),
                EntityId = id,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Uom deleted: {snapshotCode} — {snapshotName}",
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"Uom.Delete(id={id})", ct);
            return Result.Success();
        }
    }
}
