using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Cashier.GetMenu;

public static class GetMenu
{
    public sealed record Query(int TableId) : IQuery<Response>;

    public sealed record Response(
        int TableId,
        int AreaId,
        string AreaName,
        decimal ServiceChargePercent,
        decimal ServiceChargeVatPercent,
        int? PriceTableId,
        string? PriceTableName,
        DateTime ResolvedAt,
        IReadOnlyList<MenuCategory> Categories,
        IReadOnlyList<MenuItem> Items);

    public sealed record MenuCategory(
        int CategoryId,
        string Code,
        string Name,
        int? ParentId,
        string Path,
        short DisplayOrder,
        int ItemCount);

    public sealed record MenuItem(
        int ItemId,
        string Code,
        string Name,
        string? Description,
        string? ImageUrl,
        int BaseUomId,
        string UomCode,
        string UomName,
        IReadOnlyList<int> CategoryIds,
        int? KitchenStationId,
        string? KitchenStationName,
        bool IsSetMenu,
        bool IsStockable,
        bool IsAvailable,
        decimal? RawPrice,
        bool IsVatIncluded,
        decimal VatPercent,
        decimal? BasePrice,
        decimal? DisplayPrice,
        SetMenuSpec? SetMenu);

    public sealed record SetMenuSpec(
        IReadOnlyList<SetMenuMainComponent> MainComponents,
        IReadOnlyList<ChoiceCategorySpec> ChoiceCategories);

    public sealed record SetMenuMainComponent(
        int ItemId,
        string ItemName,
        decimal Quantity,
        bool IsFixed,
        string UomCode,
        string UomName);

    public sealed record ChoiceCategorySpec(
        int ChoiceCategoryId,
        string Name,
        short MinChoice,
        short? MaxChoice,
        short DisplayOrder,
        IReadOnlyList<ModifierSpec> Modifiers);

    public sealed record ModifierSpec(
        int ModifierId,
        int ItemId,
        string Name,
        decimal ExtraPrice,
        int MinPerModifier,
        int MaxPerModifier,
        short DisplayOrder);

    internal sealed class Handler(
        IDbContext db,
        IDateTimeProvider clock,
        IRoundingConfig rc,
        IMenuPriceResolver priceResolver)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var table = await db.Tables
                .Where(t => t.Id == request.TableId)
                .Select(t => new
                {
                    t.Id,
                    t.AreaId,
                    AreaName = t.Area.Name,
                    t.Area.ServiceChargePercent,
                    t.Area.ServiceChargeVatPercent
                })
                .FirstOrDefaultAsync(ct);
            if (table is null)
            {
                return Result.Failure<Response>(TableErrors.NotFound);
            }

            DateTime now = clock.UtcNow;

            // 1. Visible categories for this area (junction + subtree via Path prefix).
            List<string> areaCategoryPaths = await db.AreaMenuCategories
                .Where(amc => amc.AreaId == table.AreaId)
                .Select(amc => amc.Category.Path)
                .ToListAsync(ct);

            var allCategories = await db.Categories
                .Where(c => c.IsActive)
                .Select(c => new
                {
                    c.Id,
                    c.Code,
                    c.Name,
                    c.ParentId,
                    c.Path,
                    c.DisplayOrder
                })
                .ToListAsync(ct);

            var visibleCategories = allCategories
                .Where(c => areaCategoryPaths.Any(p => c.Path.StartsWith(p)))
                .ToList();
            // Also include ancestor categories whose ids appear in the path of any
            // visible category — e.g. "Đồ uống" (parent) is not directly linked by
            // AreaMenuCategory but is needed by the FE to build the tree.
            var ancestorIds = visibleCategories
                .SelectMany(c => c.Path.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse))
                .Distinct()
                .ToHashSet();
            var missingAncestors = allCategories
                .Where(c => ancestorIds.Contains(c.Id) && !visibleCategories.Any(vc => vc.Id == c.Id))
                .ToList();
            visibleCategories.AddRange(missingAncestors);
            var visibleCategoryIds = visibleCategories.Select(c => c.Id).ToHashSet();

            // 2. Items in those categories (junction), active.
            var itemCategoryLinks = await db.ItemCategories
                .Where(ic => visibleCategoryIds.Contains(ic.CategoryId))
                .Select(ic => new { ic.ItemId, ic.CategoryId })
                .ToListAsync(ct);
            var itemIds = itemCategoryLinks.Select(l => l.ItemId).Distinct().ToList();

            var items = await db.Items
                .Where(i => itemIds.Contains(i.Id) && i.IsActive)
                .Select(i => new
                {
                    i.Id,
                    i.Code,
                    i.Name,
                    i.Description,
                    i.ImageUrl,
                    i.BaseUomId,
                    UomCode = i.BaseUom.Code,
                    UomName = i.BaseUom.Name,
                    i.VatPercent,
                    i.KitchenStationId,
                    KitchenStationName = i.KitchenStation != null ? i.KitchenStation.Name : null,
                    i.IsStockable,
                    IsSetMenu = i.SetMenu != null
                })
                .ToListAsync(ct);
            var realItemIds = items.Select(i => i.Id).ToList();

            // 3. Resolve prices via the shared resolver (active price table by date +
            // most-specific variant wins). Same logic the cashier write flow uses.
            MenuPriceResolution resolution = await priceResolver.ResolveAsync(table.AreaId, now, realItemIds, ct);
            if (resolution.PriceTableId is null)
            {
                return Result.Failure<Response>(PriceTableErrors.NoActivePriceTable);
            }

            IReadOnlyDictionary<int, ResolvedPrice> winnerByItem = resolution.Prices;

            // Pricing spec / F2 Phase 2: an item with no price in the active table is
            // silently hidden. Keep only priced items from here on.
            var pricedItems = items.Where(i => winnerByItem.ContainsKey(i.Id)).ToList();
            var pricedIds = pricedItems.Select(i => i.Id).ToList();

            // Stock availability for stockable items.
            var stockByItem = await db.ItemStocks
                .Where(s => pricedIds.Contains(s.ItemId))
                .Select(s => new { s.ItemId, Available = s.CurrentQty - s.ReservedQty > 0 })
                .ToListAsync(ct);
            var stockAvail = stockByItem.ToDictionary(s => s.ItemId, s => s.Available);

            // Set-menu specs for set items.
            var setItemIds = pricedItems.Where(i => i.IsSetMenu).Select(i => i.Id).ToList();
            Dictionary<int, SetMenuSpec> setSpecs = await LoadSetMenuSpecsAsync(setItemIds, ct);

            // Subtree rollup (Cách A): expand each item's direct categories to include
            // their visible ancestors (parsed from Category.Path), so clicking a parent
            // category surfaces all descendant items. Intersect with the visible set so
            // categoryIds never references a category absent from the response.
            var pathByVisibleCategory = visibleCategories.ToDictionary(c => c.Id, c => c.Path);

            List<int> VisibleAncestorIds(int directCategoryId)
            {
                return pathByVisibleCategory.TryGetValue(directCategoryId, out string? path)
                    ? path.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(int.Parse)
                        .Where(visibleCategoryIds.Contains)
                        .ToList()
                    : new List<int>();
            }

            var categoryIdsByItem = itemCategoryLinks
                .Where(l => pricedIds.Contains(l.ItemId))
                .GroupBy(l => l.ItemId)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(x => VisibleAncestorIds(x.CategoryId)).Distinct().ToList());

            var itemDtos = pricedItems.Select(i =>
            {
                ResolvedPrice w = winnerByItem[i.Id]; // guaranteed present — unpriced items already dropped
                (decimal basePrice, decimal displayPrice) =
                    MenuPricing.ComputePrices(w.Price, w.IsVatIncluded, i.VatPercent, rc);

                bool available = !i.IsStockable || stockAvail.GetValueOrDefault(i.Id, true);

                return new MenuItem(
                    i.Id, i.Code, i.Name, i.Description, i.ImageUrl, i.BaseUomId,
                    i.UomCode, i.UomName,
                    categoryIdsByItem.GetValueOrDefault(i.Id) ?? new List<int>(),
                    i.KitchenStationId, i.KitchenStationName,
                    i.IsSetMenu, i.IsStockable, available,
                    w.Price, w.IsVatIncluded, i.VatPercent, basePrice, displayPrice,
                    i.IsSetMenu ? setSpecs.GetValueOrDefault(i.Id) : null);
            }).ToList();

            // itemCount per category (subtree) for UI badge — counts items whose expanded
            // (ancestor-inclusive) categoryIds contain this category, so a parent tallies
            // every descendant item.
            var itemCountByCategory = visibleCategories.ToDictionary(
                c => c.Id,
                c => categoryIdsByItem.Count(kv => kv.Value.Contains(c.Id)));

            var categoryDtos = visibleCategories
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new MenuCategory(
                    c.Id, c.Code, c.Name, c.ParentId, c.Path, c.DisplayOrder,
                    itemCountByCategory.GetValueOrDefault(c.Id)))
                .ToList();

            return Result.Success(new Response(
                table.Id, table.AreaId, table.AreaName,
                table.ServiceChargePercent, table.ServiceChargeVatPercent,
                resolution.PriceTableId, resolution.PriceTableName, now,
                categoryDtos, itemDtos));
        }

        private async Task<Dictionary<int, SetMenuSpec>> LoadSetMenuSpecsAsync(
            IReadOnlyList<int> setItemIds, CancellationToken ct)
        {
            if (setItemIds.Count == 0)
            {
                return new Dictionary<int, SetMenuSpec>();
            }

            var details = await db.SetMenuDetails
                .Where(d => setItemIds.Contains(d.SetMenuItemId))
                .OrderBy(d => d.DisplayOrder)
                .Select(d => new
                {
                    d.SetMenuItemId,
                    d.DetailType,
                    d.ComponentItemId,
                    d.ChoiceCategoryId,
                    d.Quantity,
                    d.IsFixed,
                    d.DisplayOrder,
                    ComponentName = d.ComponentItem != null ? d.ComponentItem.Name : null,
                    ComponentUomCode = d.ComponentItem != null ? d.ComponentItem.BaseUom.Code : null,
                    ComponentUomName = d.ComponentItem != null ? d.ComponentItem.BaseUom.Name : null,
                    ComponentIsActive = d.ComponentItem != null && d.ComponentItem.IsActive
                })
                .ToListAsync(ct);

            var choiceCategoryIds = details
                .Where(d => d.ChoiceCategoryId != null)
                .Select(d => d.ChoiceCategoryId!.Value).Distinct().ToList();

            var choiceCategories = await db.ChoiceCategories
                .Where(cc => choiceCategoryIds.Contains(cc.Id) && cc.IsActive)
                .Select(cc => new
                {
                    cc.Id,
                    cc.Name,
                    cc.MinChoice,
                    cc.MaxChoice,
                    cc.DisplayOrder
                })
                .ToListAsync(ct);

            var modifiers = await db.Modifiers
                .Where(m => choiceCategoryIds.Contains(m.ChoiceCategoryId) && m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new
                {
                    m.ChoiceCategoryId,
                    ModifierId = m.ItemId,
                    m.ItemId,
                    m.Item.Name,
                    m.ExtraPrice,
                    m.MinPerModifier,
                    m.MaxPerModifier,
                    m.DisplayOrder
                })
                .ToListAsync(ct);
            var modifiersByCc = modifiers.GroupBy(m => m.ChoiceCategoryId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new Dictionary<int, SetMenuSpec>();
            foreach (int setId in setItemIds)
            {
                var d = details.Where(x => x.SetMenuItemId == setId).ToList();

                var mains = d
                    .Where(x => x.DetailType == SetMenuDetailType.Component
                                && x.ComponentItemId != null && x.ComponentIsActive)
                    .Select(x => new SetMenuMainComponent(
                        x.ComponentItemId!.Value, x.ComponentName ?? "", x.Quantity ?? 0m,
                        x.IsFixed ?? false, x.ComponentUomCode ?? "", x.ComponentUomName ?? ""))
                    .ToList();

                var choices = d
                    .Where(x => x.DetailType == SetMenuDetailType.ChoiceCategory && x.ChoiceCategoryId != null)
                    .Select(x => choiceCategories.FirstOrDefault(c => c.Id == x.ChoiceCategoryId!.Value))
                    .Where(cc => cc != null) // skip choice categories deactivated since attach
                    .Select(cc =>
                    {
                        var mods = (modifiersByCc.GetValueOrDefault(cc!.Id) ?? new())
                            .Select(m => new ModifierSpec(
                                m.ModifierId, m.ItemId, m.Name, m.ExtraPrice,
                                m.MinPerModifier, m.MaxPerModifier, m.DisplayOrder))
                            .ToList();
                        return new ChoiceCategorySpec(
                            cc.Id, cc.Name, cc.MinChoice, cc.MaxChoice, cc.DisplayOrder, mods);
                    })
                    .ToList();

                result[setId] = new SetMenuSpec(mains, choices);
            }

            return result;
        }
    }
}
