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

namespace Rpom.Application.Items.CreateBomLine;

public static class CreateBomLine
{
    public sealed record Command(
        int SellableItemId,
        int MaterialItemId,
        decimal Quantity,
        int UomId,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        int SellableItemId,
        int MaterialItemId,
        decimal Quantity,
        int UomId,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.SellableItemId).GreaterThan(0);
            RuleFor(x => x.MaterialItemId).GreaterThan(0);
            RuleFor(x => x.Quantity).GreaterThan(0);
            RuleFor(x => x.UomId).GreaterThan(0);
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
            // Self-loop guard.
            if (request.SellableItemId == request.MaterialItemId)
            {
                return Result.Failure<Response>(BomLineErrors.SameItem);
            }

            // Check SellableItem exists + IsActive.
            Item? sellable = await dbContext.Items.FirstOrDefaultAsync(x => x.Id == request.SellableItemId, ct);
            if (sellable is null || !sellable.IsActive)
            {
                return Result.Failure<Response>(BomLineErrors.SellableItemNotFound);
            }

            // Check MaterialItem exists + IsActive + IsStockable + no HasRecipe.
            Item? material = await dbContext.Items.FirstOrDefaultAsync(x => x.Id == request.MaterialItemId, ct);
            if (material is null || !material.IsActive)
            {
                return Result.Failure<Response>(BomLineErrors.MaterialItemNotFound);
            }

            if (!material.IsStockable)
            {
                return Result.Failure<Response>(BomLineErrors.MaterialMustBeStockable);
            }

            if (material.HasRecipe)
            {
                return Result.Failure<Response>(BomLineErrors.MaterialAlreadyHasRecipe);
            }

            // Check Uom exists + IsActive.
            Uom? uom = await dbContext.Uoms.FirstOrDefaultAsync(x => x.Id == request.UomId && x.IsActive, ct);
            if (uom is null)
            {
                return Result.Failure<Response>(UomErrors.NotFound);
            }

            // Check no duplicate (SellableItemId, MaterialItemId).
            bool duplicate = await dbContext.BomLines
                .AnyAsync(x => x.SellableItemId == request.SellableItemId && x.MaterialItemId == request.MaterialItemId, ct);
            if (duplicate)
            {
                return Result.Failure<Response>(BomLineErrors.DuplicateMaterial);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            var entity = new BomLine
            {
                SellableItemId = request.SellableItemId,
                MaterialItemId = request.MaterialItemId,
                Quantity = request.Quantity,
                UomId = request.UomId,
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.BomLines.Add(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return Result.Failure<Response>(BomLineErrors.DuplicateMaterial);
            }

            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(BomLine),
                EntityId = entity.Id,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"BomLine created: SellableItemId={entity.SellableItemId}, MaterialItemId={entity.MaterialItemId}, qty={entity.Quantity}"
            });
            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"BomLine.Create(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.SellableItemId, entity.MaterialItemId,
                entity.Quantity, entity.UomId, entity.IsActive,
                entity.CreatedAt, entity.UpdatedAt));
        }
    }
}
