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

namespace Rpom.Application.Items.DeleteItem;

/// <summary>
/// Hard-delete an Item. Refuses if any of 7 referencing tables has rows
/// pointing to it (CartItem, OrderItem, TicketItemSum, PriceEntry, BomLine,
/// SetMenuDetail, Modifier). Owner should toggle <c>IsActive=false</c> for
/// soft retire instead.
/// </summary>
public static class DeleteItem
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
            var entity = await dbContext.Items
                .Include(x => x.ItemCategories)
                .Include(x => x.SetMenu)
                .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null) return Result.Failure(ItemErrors.NotFound);

            var id = request.Id;
            var inUse =
                await dbContext.CartItems.AnyAsync(x => x.ItemId == id, ct)
                || await dbContext.OrderItems.AnyAsync(x => x.ItemId == id, ct)
                || await dbContext.TicketItemSums.AnyAsync(x => x.ItemId == id, ct)
                || await dbContext.PriceEntries.AnyAsync(x => x.ItemId == id, ct)
                || await dbContext.BomLines.AnyAsync(x => x.SellableItemId == id || x.MaterialItemId == id, ct)
                || await dbContext.SetMenuDetails.AnyAsync(x => x.SetMenuItemId == id || x.ComponentItemId == id, ct)
                || await dbContext.Modifiers.AnyAsync(x => x.ItemId == id, ct);

            if (inUse) return Result.Failure(ItemErrors.InUse);

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;
            var snapshotCode = entity.Code;
            var snapshotName = entity.Name;

            // Drop ItemCategory junction explicitly (cascade may not be configured).
            dbContext.ItemCategories.RemoveRange(entity.ItemCategories);
            if (entity.SetMenu is not null) dbContext.SetMenus.Remove(entity.SetMenu);
            dbContext.Items.Remove(entity);

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Item),
                EntityId = id,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Item deleted: {snapshotCode} — {snapshotName}",
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"Item.Delete(id={id})", ct);
            return Result.Success();
        }
    }
}
