using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Configuration;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Items.ListItems;

/// <summary>
///     List items (paginated) with optional Category descendant filter — items
///     whose ItemCategory.CategoryId matches the category or any descendant
///     (resolved via Category.Path LIKE).
/// </summary>
public static class ListItems
{
    public sealed record Query(
        int? CategoryId,
        string? Search,
        bool? IsActive,
        int PageNumber,
        int PageSize) : IQuery<Page<Item>>;

    public sealed record Item(
        int Id,
        string Code,
        string Name,
        string? ImageUrl,
        string BaseUomCode,
        decimal VatPercent,
        bool IsStockable,
        bool HasRecipe,
        bool IsActive,
        bool IsSetMenu,
        IReadOnlyList<string> CategoryNames,
        int? PrimaryCategoryId,
        string? PrimaryCategoryName);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator(IConfigValueService config)
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize)
                .MustAsync(async (size, ct) =>
                    size <= await config.GetIntAsync(ConfigCodes.PaginationMaxPageSize, 500, ct))
                .WithMessage("PageSize vượt quá giới hạn tối đa cho phép.");
        }
    }

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Page<Item>>
    {
        public async Task<Result<Page<Item>>> Handle(Query request, CancellationToken ct)
        {
            IQueryable<Domain.Menu.Item> q = dbContext.Items.AsQueryable();

            if (request.IsActive.HasValue)
            {
                q = q.Where(x => x.IsActive == request.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                string s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Code.ToLower().Contains(s) || x.Name.ToLower().Contains(s));
            }

            if (request.CategoryId.HasValue)
            {
                var root = await dbContext.Categories
                    .Where(c => c.Id == request.CategoryId.Value)
                    .Select(c => new { c.Id, c.Path })
                    .FirstOrDefaultAsync(ct);

                if (root is null)
                {
                    return Result.Success(new Page<Item>(Array.Empty<Item>(), 0, request.PageNumber, request.PageSize));
                }

                List<int> idsInSubtree = await dbContext.Categories
                    .Where(c => c.Id == root.Id || EF.Functions.Like(c.Path, root.Path + "%"))
                    .Select(c => c.Id)
                    .ToListAsync(ct);

                q = q.Where(x => x.ItemCategories.Any(ic => idsInSubtree.Contains(ic.CategoryId)));
            }

            int totalCount = await q.CountAsync(ct);

            var rows = await q
                .OrderBy(x => x.Name)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
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
                    IsSetMenu = x.SetMenu != null,
                    CategoryNames = x.ItemCategories.Select(ic => ic.Category.Name).ToList(),
                    PrimaryCategoryId = x.ItemCategories
                        .Where(ic => ic.IsMain)
                        .Select(ic => (int?)ic.CategoryId).FirstOrDefault(),
                    PrimaryCategoryName = x.ItemCategories
                        .Where(ic => ic.IsMain)
                        .Select(ic => ic.Category.Name).FirstOrDefault()
                })
                .ToListAsync(ct);

            var items = rows
                .Select(r => new Item(
                    r.Id, r.Code, r.Name, r.ImageUrl, r.BaseUomCode, r.VatPercent,
                    r.IsStockable, r.HasRecipe, r.IsActive, r.IsSetMenu,
                    r.CategoryNames, r.PrimaryCategoryId, r.PrimaryCategoryName))
                .ToList();

            return Result.Success(new Page<Item>(items, totalCount, request.PageNumber, request.PageSize));
        }
    }
}
