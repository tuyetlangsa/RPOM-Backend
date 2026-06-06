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

namespace Rpom.Application.PriceTables.UpdatePriceTable;

public static class UpdatePriceTable
{
    public sealed record Command(
        int Id,
        string Code,
        string Name,
        string? Description,
        DateOnly? BeginDate,
        DateOnly? EndDate,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
        DateOnly? BeginDate,
        DateOnly? EndDate,
        bool IsActive,
        int VariantCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Description).MaximumLength(500);
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
            var entity = await dbContext.PriceTables.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null) return Result.Failure<Response>(PriceTableErrors.NotFound);

            if (request.BeginDate.HasValue && request.EndDate.HasValue
                && request.BeginDate.Value > request.EndDate.Value)
                return Result.Failure<Response>(PriceTableErrors.DateRangeInvalid);

            var code = request.Code.Trim();
            var codeLower = code.ToLower();
            var duplicate = await dbContext.PriceTables
                .AnyAsync(x => x.Id != request.Id && x.Code.ToLower() == codeLower, ct);
            if (duplicate) return Result.Failure<Response>(PriceTableErrors.CodeDuplicate);

            var now = clock.UtcNow;
            var staffId = currentStaff.StaffAccountId;
            var summary = BuildSummary(entity, request, code);

            entity.Code = code;
            entity.Name = request.Name.Trim();
            entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            entity.BeginDate = request.BeginDate;
            entity.EndDate = request.EndDate;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = now;

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(PriceTable),
                EntityId = entity.Id,
                Action = "UPDATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = summary,
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"PriceTable.Update(id={entity.Id})", ct);

            var variantCount = await dbContext.PriceVariants.CountAsync(v => v.PriceTableId == entity.Id, ct);
            return Result.Success(new Response(
                entity.Id, entity.Code, entity.Name, entity.Description,
                entity.BeginDate, entity.EndDate, entity.IsActive,
                variantCount, entity.CreatedAt, entity.UpdatedAt));
        }

        private static string BuildSummary(PriceTable before, Command after, string normalizedCode)
        {
            var diffs = new List<string>();
            if (before.Code != normalizedCode)
                diffs.Add($"code: '{before.Code}' → '{normalizedCode}'");
            if (before.Name != after.Name.Trim())
                diffs.Add($"name: '{before.Name}' → '{after.Name.Trim()}'");
            if ((before.Description ?? "") != (after.Description?.Trim() ?? ""))
                diffs.Add("description changed");
            if (before.BeginDate != after.BeginDate)
                diffs.Add($"beginDate: {before.BeginDate?.ToString() ?? "null"} → {after.BeginDate?.ToString() ?? "null"}");
            if (before.EndDate != after.EndDate)
                diffs.Add($"endDate: {before.EndDate?.ToString() ?? "null"} → {after.EndDate?.ToString() ?? "null"}");
            if (before.IsActive != after.IsActive)
                diffs.Add($"isActive: {before.IsActive} → {after.IsActive}");
            return diffs.Count == 0 ? "PriceTable updated (no changes)" : string.Join("; ", diffs);
        }
    }
}
