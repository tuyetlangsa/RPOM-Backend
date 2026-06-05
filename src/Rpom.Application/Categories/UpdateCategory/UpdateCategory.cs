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

namespace Rpom.Application.Categories.UpdateCategory;

/// <summary>
/// Rename / move Category. Moving cascades Path + Level recompute through every
/// descendant in a single SaveChanges so the tree stays consistent.
/// </summary>
public static class UpdateCategory
{
    public sealed record Command(
        int Id,
        string Code,
        string Name,
        string? Description,
        int? ParentId,
        short DisplayOrder,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
        int? ParentId,
        string Path,
        short Level,
        short DisplayOrder,
        bool IsActive,
        int ItemCount,
        int ChildCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(500);
            RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo((short)0);
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
            var entity = await dbContext.Categories.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null) return Result.Failure<Response>(CategoryErrors.NotFound);

            var code = request.Code.Trim();
            var codeLower = code.ToLower();
            var duplicate = await dbContext.Categories
                .AnyAsync(x => x.Id != request.Id && x.Code.ToLower() == codeLower, ct);
            if (duplicate) return Result.Failure<Response>(CategoryErrors.CodeDuplicate);

            // Parent change — validate before we touch anything.
            Category? newParent = null;
            var parentChanged = entity.ParentId != request.ParentId;
            if (request.ParentId.HasValue)
            {
                if (request.ParentId.Value == entity.Id)
                    return Result.Failure<Response>(CategoryErrors.ParentSelf);

                newParent = await dbContext.Categories
                    .FirstOrDefaultAsync(x => x.Id == request.ParentId.Value, ct);
                if (newParent is null) return Result.Failure<Response>(CategoryErrors.ParentNotFound);

                if (CategoryTreeHelpers.WouldCreateCycle(newParent, entity.Id))
                    return Result.Failure<Response>(CategoryErrors.ParentCycle);
            }

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;
            var summary = BuildSummary(entity, request, code);

            var oldPath = entity.Path;
            var oldLevel = entity.Level;

            entity.Code = code;
            entity.Name = request.Name.Trim();
            entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            entity.DisplayOrder = request.DisplayOrder;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = now;

            if (parentChanged)
            {
                entity.ParentId = request.ParentId;
                entity.Level = CategoryTreeHelpers.ComputeLevel(newParent);
                entity.Path = CategoryTreeHelpers.ComputePath(newParent, entity.Id);
                await CategoryTreeHelpers.RecomputeDescendantsAsync(dbContext, entity, oldPath, oldLevel, ct);
            }

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Category),
                EntityId = entity.Id,
                Action = "UPDATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = summary,
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"Category.Update(id={entity.Id})", ct);

            var itemCount = await dbContext.ItemCategories.CountAsync(ic => ic.CategoryId == entity.Id, ct);
            var childCount = await dbContext.Categories.CountAsync(c => c.ParentId == entity.Id, ct);

            return Result.Success(new Response(
                entity.Id, entity.Code, entity.Name, entity.Description, entity.ParentId,
                entity.Path, entity.Level, entity.DisplayOrder, entity.IsActive,
                itemCount, childCount, entity.CreatedAt, entity.UpdatedAt));
        }

        private static string BuildSummary(Category before, Command after, string normalizedCode)
        {
            var diffs = new List<string>();
            if (before.Code != normalizedCode)
                diffs.Add($"code: '{before.Code}' → '{normalizedCode}'");
            if (before.Name != after.Name.Trim())
                diffs.Add($"name: '{before.Name}' → '{after.Name.Trim()}'");
            if ((before.Description ?? "") != (after.Description?.Trim() ?? ""))
                diffs.Add("description changed");
            if (before.ParentId != after.ParentId)
                diffs.Add($"parent: {before.ParentId?.ToString() ?? "root"} → {after.ParentId?.ToString() ?? "root"}");
            if (before.DisplayOrder != after.DisplayOrder)
                diffs.Add($"displayOrder: {before.DisplayOrder} → {after.DisplayOrder}");
            if (before.IsActive != after.IsActive)
                diffs.Add($"isActive: {before.IsActive} → {after.IsActive}");
            return diffs.Count == 0 ? "Category updated (no changes)" : string.Join("; ", diffs);
        }
    }
}
