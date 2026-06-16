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

namespace Rpom.Application.Items.DeleteUomConversion;

public static class DeleteUomConversion
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
            ItemUomConversion? entity = await dbContext.ItemUomConversions
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.ItemId == request.ItemId, ct);
            if (entity is null)
            {
                return Result.Failure(ItemUomConversionErrors.NotFound);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            dbContext.ItemUomConversions.Remove(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(ItemUomConversion),
                EntityId = request.Id,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"ItemUomConversion deleted: ItemId={request.ItemId}, UomId={entity.UomId}"
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"ItemUomConversion.Delete(id={request.Id})", ct);
            return Result.Success();
        }
    }
}
