using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Kitchen.GetKitchenMenu;
public static class GetKitchenMenu
{
    public sealed record Query(int AreaId) : IQuery<Response>;

    public sealed record Response(
        int AreaId,
        string AreaName,
        int KitchenStationId,
        IReadOnlyList<MenuCategory> Categories,
        IReadOnlyList<MenuItem> Items);

    public sealed record MenuCategory(
        int CategoryId,
        string Code,
        string Name,
        int? ParentId,
        string Path,
        short DisplayOrder);

    public sealed record MenuItem(
        int ItemId,
        string Code,
        string Name,
        string? Description,
        string? ImageUrl,
        string UomCode,
        string UomName,
        IReadOnlyList<int> CategoryIds,
        bool IsStockable,
        bool InStock,
        bool IsLocked,
        string? LockNote,
        DateTime? LockedAt);

    internal sealed class Handler(IDbContext db, ICurrentStaff currentStaff)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            int? stationId = currentStaff.KitchenStationId;
            if (stationId is null) return Result.Failure<Response>(KitchenStationErrors.NotSelected);

            var area = await db.Areas
                .Where(a => a.Id == request.AreaId)
                .Select(a => new { a.Id, a.Name })
                .FirstOrDefaultAsync(ct);
            if (area is null) return Result.Failure<Response>(AreaErrors.NotFound);

            // 1. Categories visible cho area (junction + subtree qua Path prefix) + tổ tiên.
            List<string> areaCategoryPaths = await db.AreaMenuCategories
                .Where(amc => amc.AreaId == area.Id)
                .Select(amc => amc.Category.Path)
                .ToListAsync(ct);

            var allCategories = await db.Categories
                .Where(c => c.IsActive)
                .Select(c => new { c.Id, c.Code, c.Name, c.ParentId, c.Path, c.DisplayOrder })
                .ToListAsync(ct);

            var visibleCategories = allCategories
                .Where(c => areaCategoryPaths.Any(p => c.Path.StartsWith(p)))
                .ToList();
            var ancestorIds = visibleCategories
                .SelectMany(c => c.Path.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse))
                .Distinct().ToHashSet();
            visibleCategories.AddRange(allCategories
                .Where(c => ancestorIds.Contains(c.Id) && visibleCategories.All(vc => vc.Id != c.Id)));
            var visibleCategoryIds = visibleCategories.Select(c => c.Id).ToHashSet();

            // 2. Items trong các category đó, active, VÀ thuộc đúng khu bếp của phiên.
            var itemCategoryLinks = await db.ItemCategories
                .Where(ic => visibleCategoryIds.Contains(ic.CategoryId))
                .Select(ic => new { ic.ItemId, ic.CategoryId })
                .ToListAsync(ct);
            var itemIds = itemCategoryLinks.Select(l => l.ItemId).Distinct().ToList();

            var items = await db.Items
                .Where(i => itemIds.Contains(i.Id) && i.IsActive && i.KitchenStationId == stationId.Value)
                .Select(i => new
                {
                    i.Id,
                    i.Code,
                    i.Name,
                    i.Description,
                    i.ImageUrl,
                    UomCode = i.BaseUom.Code,
                    UomName = i.BaseUom.Name,
                    i.IsStockable
                })
                .ToListAsync(ct);
            var realItemIds = items.Select(i => i.Id).ToList();

            // 3. Tồn kho (cho món stockable) + khoá tại area này.
            var stockAvail = (await db.ItemStocks
                    .Where(s => realItemIds.Contains(s.ItemId))
                    .Select(s => new { s.ItemId, Available = s.CurrentQty - s.ReservedQty > 0 })
                    .ToListAsync(ct))
                .ToDictionary(s => s.ItemId, s => s.Available);

            var locks = await db.ItemAreaLocks
                .Where(l => l.AreaId == area.Id && realItemIds.Contains(l.ItemId))
                .Select(l => new { l.ItemId, l.Note, l.LockedAt })
                .ToListAsync(ct);
            var lockByItem = locks.ToDictionary(l => l.ItemId);

            var categoryIdsByItem = itemCategoryLinks
                .Where(l => realItemIds.Contains(l.ItemId))
                .GroupBy(l => l.ItemId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.CategoryId).Distinct().ToList());

            var itemDtos = items.Select(i =>
            {
                bool inStock = !i.IsStockable || stockAvail.GetValueOrDefault(i.Id, true);
                lockByItem.TryGetValue(i.Id, out var lk);
                return new MenuItem(
                    i.Id, i.Code, i.Name, i.Description, i.ImageUrl, i.UomCode, i.UomName,
                    categoryIdsByItem.GetValueOrDefault(i.Id) ?? [],
                    i.IsStockable, inStock,
                    lk is not null, lk?.Note, lk?.LockedAt);
            }).ToList();

            // Chỉ giữ category có item của bếp (để bếp không thấy nhánh rỗng); kèm tổ tiên của chúng.
            var keepCatIds = categoryIdsByItem.Values.SelectMany(x => x).ToHashSet();
            var keepWithAncestors = visibleCategories
                .Where(c => keepCatIds.Contains(c.Id))
                .SelectMany(c => c.Path.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse))
                .ToHashSet();
            var categoryDtos = visibleCategories
                .Where(c => keepWithAncestors.Contains(c.Id))
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new MenuCategory(c.Id, c.Code, c.Name, c.ParentId, c.Path, c.DisplayOrder))
                .ToList();

            return Result.Success(new Response(
                area.Id, area.Name, stationId.Value, categoryDtos, itemDtos));
        }
    }
}
