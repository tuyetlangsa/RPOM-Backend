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

namespace Rpom.Application.Items.UpdateItem;

/// <summary>
/// Update item master fields + replace its ItemCategory assignments.
/// Code is editable (snapshots in CartItem/OrderItem preserve history).
/// </summary>
public static class UpdateItem
{
    public sealed record CategoryInput(int CategoryId, bool IsMain);

    public sealed record Command(
        int Id,
        string Code,
        string Name,
        string? Description,
        string? ImageUrl,
        int BaseUomId,
        decimal VatPercent,
        bool IsStockable,
        bool HasRecipe,
        decimal? LowStockThreshold,
        int? KitchenStationId,
        bool IsActive,
        IReadOnlyList<CategoryInput> Categories) : ICommand<Response>;

    public sealed record CategoryAssignment(int CategoryId, string Name, bool IsMain);

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
        string? ImageUrl,
        int BaseUomId,
        string BaseUomCode,
        string BaseUomName,
        decimal VatPercent,
        bool IsStockable,
        bool HasRecipe,
        decimal? LowStockThreshold,
        int? KitchenStationId,
        string? KitchenStationName,
        bool IsActive,
        IReadOnlyList<CategoryAssignment> Categories,
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
            RuleFor(x => x.ImageUrl).MaximumLength(500);
            RuleFor(x => x.BaseUomId).GreaterThan(0);
            RuleFor(x => x.VatPercent).InclusiveBetween(0m, 100m);
            RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0m).When(x => x.LowStockThreshold.HasValue);
            RuleFor(x => x.Categories).NotEmpty().WithMessage("Hàng hoá phải thuộc ít nhất 1 nhóm.");
            RuleFor(x => x.Categories).Must(cs => cs.Count(c => c.IsMain) == 1)
                .WithMessage("Phải chỉ định đúng 1 nhóm chính (IsMain).");
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
            var entity = await dbContext.Items
                .Include(x => x.ItemCategories)
                .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null) return Result.Failure<Response>(ItemErrors.NotFound);

            var code = request.Code.Trim();
            var codeLower = code.ToLower();
            var duplicate = await dbContext.Items
                .AnyAsync(x => x.Id != request.Id && x.Code.ToLower() == codeLower, ct);
            if (duplicate) return Result.Failure<Response>(ItemErrors.CodeDuplicate);

            var uom = await dbContext.Uoms
                .Where(u => u.Id == request.BaseUomId && u.IsActive)
                .Select(u => new { u.Id, u.Code, u.Name })
                .FirstOrDefaultAsync(ct);
            if (uom is null) return Result.Failure<Response>(ItemErrors.UomNotFound);

            string? stationName = null;
            if (request.KitchenStationId.HasValue)
            {
                var st = await dbContext.KitchenStations
                    .Where(s => s.Id == request.KitchenStationId.Value && s.IsActive)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync(ct);
                if (st is null) return Result.Failure<Response>(ItemErrors.KitchenStationNotFound);
                stationName = st;
            }

            var categoryIds = request.Categories.Select(c => c.CategoryId).Distinct().ToList();
            var cats = await dbContext.Categories
                .Where(c => categoryIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToListAsync(ct);
            if (cats.Count != categoryIds.Count)
                return Result.Failure<Response>(ItemErrors.CategoryNotFound);

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;
            var summary = BuildSummary(entity, request, code);

            entity.Code = code;
            entity.Name = request.Name.Trim();
            entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            entity.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
            entity.BaseUomId = request.BaseUomId;
            entity.VatPercent = request.VatPercent;
            entity.IsStockable = request.IsStockable;
            entity.HasRecipe = request.HasRecipe;
            entity.LowStockThreshold = request.LowStockThreshold;
            entity.KitchenStationId = request.KitchenStationId;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = now;

            // Replace junction — simplest: remove all + add desired set.
            dbContext.ItemCategories.RemoveRange(entity.ItemCategories);
            entity.ItemCategories.Clear();
            foreach (var c in request.Categories)
            {
                entity.ItemCategories.Add(new ItemCategory
                {
                    ItemId = entity.Id,
                    CategoryId = c.CategoryId,
                    IsMain = c.IsMain,
                    CreatedAt = now,
                });
            }

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Item),
                EntityId = entity.Id,
                Action = "UPDATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = summary,
            });

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return Result.Failure<Response>(ItemErrors.CodeDuplicate);
            }
            await versionService.BumpAsync(VersionScopes.Menu, $"Item.Update(id={entity.Id})", ct);

            var byId = cats.ToDictionary(c => c.Id, c => c.Name);
            var assignments = request.Categories
                .Select(c => new CategoryAssignment(c.CategoryId, byId[c.CategoryId], c.IsMain))
                .ToList();

            return Result.Success(new Response(
                entity.Id, entity.Code, entity.Name, entity.Description, entity.ImageUrl,
                entity.BaseUomId, uom.Code, uom.Name,
                entity.VatPercent, entity.IsStockable, entity.HasRecipe, entity.LowStockThreshold,
                entity.KitchenStationId, stationName, entity.IsActive,
                assignments, entity.CreatedAt, entity.UpdatedAt));
        }

        private static string BuildSummary(Item before, Command after, string normalizedCode)
        {
            var diffs = new List<string>();
            if (before.Code != normalizedCode) diffs.Add($"code: '{before.Code}' → '{normalizedCode}'");
            if (before.Name != after.Name.Trim()) diffs.Add($"name: '{before.Name}' → '{after.Name.Trim()}'");
            if ((before.Description ?? "") != (after.Description?.Trim() ?? "")) diffs.Add("description changed");
            if ((before.ImageUrl ?? "") != (after.ImageUrl?.Trim() ?? "")) diffs.Add("image changed");
            if (before.BaseUomId != after.BaseUomId) diffs.Add($"baseUom: {before.BaseUomId} → {after.BaseUomId}");
            if (before.VatPercent != after.VatPercent) diffs.Add($"vat: {before.VatPercent} → {after.VatPercent}");
            if (before.IsStockable != after.IsStockable) diffs.Add($"isStockable: {before.IsStockable} → {after.IsStockable}");
            if (before.HasRecipe != after.HasRecipe) diffs.Add($"hasRecipe: {before.HasRecipe} → {after.HasRecipe}");
            if (before.KitchenStationId != after.KitchenStationId)
                diffs.Add($"kitchenStation: {before.KitchenStationId?.ToString() ?? "null"} → {after.KitchenStationId?.ToString() ?? "null"}");
            if (before.IsActive != after.IsActive) diffs.Add($"isActive: {before.IsActive} → {after.IsActive}");
            diffs.Add($"categories replaced (n={after.Categories.Count})");
            return string.Join("; ", diffs);
        }
    }
}
