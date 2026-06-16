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

namespace Rpom.Application.Items.UpdateBomLine;

public static class UpdateBomLine
{
    public sealed record Command(
        int Id,
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
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Quantity).GreaterThan(0);
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
            BomLine? entity = await dbContext.BomLines
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.SellableItemId == request.SellableItemId, ct);
            if (entity is null)
            {
                return Result.Failure<Response>(BomLineErrors.NotFound);
            }

            // MaterialItemId cannot change.
            if (request.MaterialItemId != entity.MaterialItemId)
            {
                return Result.Failure<Response>(Error.BadRequest(
                    "BomLine.CannotChangeMaterial",
                    "Không thể thay đổi nguyên liệu của dòng công thức."));
            }

            // UomId cannot change.
            if (request.UomId != entity.UomId)
            {
                return Result.Failure<Response>(Error.BadRequest(
                    "BomLine.CannotChangeUom",
                    "Không thể thay đổi đơn vị tính của dòng công thức."));
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;
            string summary = BuildSummary(entity, request);

            entity.Quantity = request.Quantity;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = now;

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(BomLine),
                EntityId = entity.Id,
                Action = "UPDATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = summary
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"BomLine.Update(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.SellableItemId, entity.MaterialItemId,
                entity.Quantity, entity.UomId, entity.IsActive,
                entity.CreatedAt, entity.UpdatedAt));
        }

        private static string BuildSummary(BomLine before, Command after)
        {
            var diffs = new List<string>();
            if (before.Quantity != after.Quantity)
            {
                diffs.Add($"quantity: {before.Quantity} → {after.Quantity}");
            }

            if (before.IsActive != after.IsActive)
            {
                diffs.Add($"isActive: {before.IsActive} → {after.IsActive}");
            }

            return diffs.Count == 0
                ? "BomLine updated (no changes)"
                : string.Join("; ", diffs);
        }
    }
}
