using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Tables.UpdateTable;

/// <summary>
/// Update master-data fields on a Table. Status is NOT updatable here —
/// it is driven by Ticket lifecycle in a separate command.
/// </summary>
public static class UpdateTable
{
    public sealed record Command(
        int Id,
        int AreaId,
        string Code,
        int SeatCount,
        string? Description,
        bool IsActive) : ICommand<TableItem>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.AreaId).GreaterThan(0);
            RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
            RuleFor(x => x.SeatCount).GreaterThan(0);
            RuleFor(x => x.Description).MaximumLength(500);
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock) : ICommandHandler<Command, TableItem>
    {
        public async Task<Result<TableItem>> Handle(Command request, CancellationToken ct)
        {
            var entity = await dbContext.Tables.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null) return Result.Failure<TableItem>(TableErrors.NotFound);

            if (entity.AreaId != request.AreaId)
            {
                var areaExists = await dbContext.Areas.AnyAsync(x => x.Id == request.AreaId, ct);
                if (!areaExists) return Result.Failure<TableItem>(TableErrors.AreaNotFound);
            }

            var code = request.Code.Trim();
            if (entity.AreaId != request.AreaId || entity.Code != code)
            {
                var dup = await dbContext.Tables.AnyAsync(
                    x => x.Id != request.Id && x.AreaId == request.AreaId && x.Code == code, ct);
                if (dup) return Result.Failure<TableItem>(TableErrors.CodeDuplicateInArea);
            }

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;
            var summary = BuildSummary(entity, request, code);

            entity.AreaId = request.AreaId;
            entity.Code = code;
            entity.SeatCount = request.SeatCount;
            entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = now;

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Table),
                EntityId = entity.Id,
                Action = "UPDATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = summary,
            });

            await dbContext.SaveChangesAsync(ct);

            return Result.Success(new TableItem(
                entity.Id, entity.AreaId, entity.Code, entity.SeatCount, entity.Description,
                entity.Status, entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }

        private static string BuildSummary(Table before, Command after, string normalizedCode)
        {
            var diffs = new List<string>();
            if (before.AreaId != after.AreaId)
                diffs.Add($"areaId: {before.AreaId} → {after.AreaId}");
            if (before.Code != normalizedCode)
                diffs.Add($"code: '{before.Code}' → '{normalizedCode}'");
            if (before.SeatCount != after.SeatCount)
                diffs.Add($"seatCount: {before.SeatCount} → {after.SeatCount}");
            if ((before.Description ?? "") != (after.Description?.Trim() ?? ""))
                diffs.Add("description changed");
            if (before.IsActive != after.IsActive)
                diffs.Add($"isActive: {before.IsActive} → {after.IsActive}");
            return diffs.Count == 0 ? "Table updated (no changes)" : string.Join("; ", diffs);
        }
    }
}
