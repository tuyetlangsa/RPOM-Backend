using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Items.ListItems;

/// <summary>
/// List items with optional Category descendant filter (categoryId selects
/// items whose ItemCategory.CategoryId is the category itself or any of its
/// descendants — resolved via Category.Path LIKE).
/// Thin row shape — no per-row joins beyond category/uom display names.
/// </summary>
public static class ListItems
{
    public sealed record Query(int? CategoryId, string? Search, bool? IsActive)
        : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? ImageUrl,
        string BaseUomCode,
        decimal VatPercent,
        bool IsStockable,
        bool HasRecipe,
        bool IsActive,
        IReadOnlyList<string> CategoryNames,
        int? PrimaryCategoryId,
        string? PrimaryCategoryName);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            var q = dbContext.Items.AsQueryable();

            if (request.IsActive.HasValue) q = q.Where(x => x.IsActive == request.IsActive.Value);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Code.ToLower().Contains(s) || x.Name.ToLower().Contains(s));
            }

            if (request.CategoryId.HasValue)
            {
                // Resolve descendant ids via Category.Path prefix.
                var root = await dbContext.Categories
                    .Where(c => c.Id == request.CategoryId.Value)
                    .Select(c => new { c.Id, c.Path })
                    .FirstOrDefaultAsync(ct);

                if (root is null) return Result.Success<IReadOnlyList<Response>>(Array.Empty<Response>());

                var idsInSubtree = await dbContext.Categories
                    .Where(c => c.Id == root.Id || EF.Functions.Like(c.Path, root.Path + "%"))
                    .Select(c => c.Id)
                    .ToListAsync(ct);

                q = q.Where(x => x.ItemCategories.Any(ic => idsInSubtree.Contains(ic.CategoryId)));
            }

            var rows = await q
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Name,
                    x.ImageUrl,
                    BaseUomCode = x.BaseUom.Code,
                    x.VatPercent,
                    x.IsStockable,
                    x.HasRecipe,
                    x.IsActive,
                    CategoryNames = x.ItemCategories.Select(ic => ic.Category.Name).ToList(),
                    PrimaryCategoryId = x.ItemCategories
                        .Where(ic => ic.IsMain)
                        .Select(ic => (int?)ic.CategoryId).FirstOrDefault(),
                    PrimaryCategoryName = x.ItemCategories
                        .Where(ic => ic.IsMain)
                        .Select(ic => ic.Category.Name).FirstOrDefault(),
                })
                .ToListAsync(ct);

            var result = rows
                .Select(r => new Response(
                    r.Id, r.Code, r.Name, r.ImageUrl, r.BaseUomCode, r.VatPercent,
                    r.IsStockable, r.HasRecipe, r.IsActive,
                    r.CategoryNames, r.PrimaryCategoryId, r.PrimaryCategoryName))
                .ToList();

            return Result.Success<IReadOnlyList<Response>>(result);
        }
    }
}
