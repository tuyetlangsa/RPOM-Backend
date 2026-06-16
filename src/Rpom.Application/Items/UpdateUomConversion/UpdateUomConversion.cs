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

namespace Rpom.Application.Items.UpdateUomConversion;

public static class UpdateUomConversion
{
    public sealed record Command(
        int Id,
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
            RuleFor(x => x.Id).GreaterThan(0);
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
            ItemUomConversion? entity = await dbContext.ItemUomConversions
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.ItemId == request.ItemId, ct);
            if (entity is null)
            {
                return Result.Failure<Response>(ItemUomConversionErrors.NotFound);
            }

            // UomId cannot change.
            if (request.UomId != entity.UomId)
            {
                return Result.Failure<Response>(Error.BadRequest(
                    "ItemUomConversion.CannotChangeUom",
                    "Không thể thay đổi đơn vị tính của quy đổi."));
            }

            // Re-verify Item still exists + IsActive.
            Item? item = await dbContext.Items.FirstOrDefaultAsync(x => x.Id == request.ItemId && x.IsActive, ct);
            if (item is null)
            {
                return Result.Failure<Response>(ItemErrors.NotFound);
            }

            // Re-verify Uom still exists + IsActive.
            Uom? uom = await dbContext.Uoms.FirstOrDefaultAsync(x => x.Id == request.UomId && x.IsActive, ct);
            if (uom is null)
            {
                return Result.Failure<Response>(UomErrors.NotFound);
            }

            // Check duplicate for other records with same (ItemId, UomId).
            bool duplicate = await dbContext.ItemUomConversions
                .AnyAsync(x => x.Id != request.Id && x.ItemId == request.ItemId && x.UomId == request.UomId, ct);
            if (duplicate)
            {
                return Result.Failure<Response>(ItemUomConversionErrors.DuplicateUom);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;
            string summary = BuildSummary(entity, request);

            entity.ItemId = request.ItemId;
            entity.UomId = request.UomId;
            entity.FactorToBase = request.FactorToBase;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = now;

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(ItemUomConversion),
                EntityId = entity.Id,
                Action = "UPDATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = summary
            });

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return Result.Failure<Response>(ItemUomConversionErrors.DuplicateUom);
            }

            await versionService.BumpAsync(VersionScopes.Menu, $"ItemUomConversion.Update(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.ItemId, entity.UomId, entity.FactorToBase,
                entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }

        private static string BuildSummary(ItemUomConversion before, Command after)
        {
            var diffs = new List<string>();
            if (before.FactorToBase != after.FactorToBase)
            {
                diffs.Add($"factorToBase: {before.FactorToBase} → {after.FactorToBase}");
            }

            if (before.IsActive != after.IsActive)
            {
                diffs.Add($"isActive: {before.IsActive} → {after.IsActive}");
            }

            return diffs.Count == 0
                ? "ItemUomConversion updated (no changes)"
                : string.Join("; ", diffs);
        }
    }
}
