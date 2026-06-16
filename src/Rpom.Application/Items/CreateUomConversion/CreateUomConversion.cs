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
using Rpom.Domain.Menu;

namespace Rpom.Application.Items.CreateUomConversion;

public static class CreateUomConversion
{
    public sealed record Command(
        int ItemId,
        int UomId,
        decimal FactorToBase,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        int ItemId,
        int UomId,
        decimal FactorToBase,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ItemId).GreaterThan(0);
            RuleFor(x => x.UomId).GreaterThan(0);
            RuleFor(x => x.FactorToBase).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            // Check Item exists + IsActive.
            Item? item = await dbContext.Items.FirstOrDefaultAsync(x => x.Id == request.ItemId, ct);
            if (item is null || !item.IsActive)
            {
                return Result.Failure<Response>(ItemErrors.NotFound);
            }

            // Check Uom exists + IsActive.
            Uom? uom = await dbContext.Uoms.FirstOrDefaultAsync(x => x.Id == request.UomId, ct);
            if (uom is null || !uom.IsActive)
            {
                return Result.Failure<Response>(UomErrors.NotFound);
            }

            // Check no existing (ItemId, UomId) duplicate.
            bool duplicate = await dbContext.ItemUomConversions
                .AnyAsync(x => x.ItemId == request.ItemId && x.UomId == request.UomId, ct);
            if (duplicate)
            {
                return Result.Failure<Response>(ItemUomConversionErrors.DuplicateUom);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            var entity = new ItemUomConversion
            {
                ItemId = request.ItemId,
                UomId = request.UomId,
                FactorToBase = request.FactorToBase,
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.ItemUomConversions.Add(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Race condition safety net — DB unique index caught what pre-check missed.
                return Result.Failure<Response>(ItemUomConversionErrors.DuplicateUom);
            }

            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(ItemUomConversion),
                EntityId = entity.Id,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"ItemUomConversion created: factorToBase={entity.FactorToBase} from {uom.Code}"
            });
            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"ItemUomConversion.Create(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.ItemId, entity.UomId, entity.FactorToBase,
                entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }
    }
}
