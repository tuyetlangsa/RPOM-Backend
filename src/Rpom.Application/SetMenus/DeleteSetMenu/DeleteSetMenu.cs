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

namespace Rpom.Application.SetMenus.DeleteSetMenu;

/// <summary>
///     Remove the SET_MENU aspect of an Item — deletes the SetMenu row (details
///     cascade), reverting the Item to SINGLE. The Item master row is untouched.
/// </summary>
public static class DeleteSetMenu
{
    public sealed record Command(int ItemId) : ICommand;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ItemId).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            SetMenu? setMenu = await db.SetMenus.FirstOrDefaultAsync(s => s.ItemId == request.ItemId, ct);
            if (setMenu is null)
            {
                return Result.Failure(SetMenuErrors.NotASetMenu);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            db.SetMenus.Remove(setMenu);

            StaffAccount staff = await db.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(SetMenu),
                EntityId = request.ItemId,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"SetMenu removed from item {request.ItemId}"
            });

            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"SetMenu.Delete(itemId={request.ItemId})", ct);
            return Result.Success();
        }
    }
}
