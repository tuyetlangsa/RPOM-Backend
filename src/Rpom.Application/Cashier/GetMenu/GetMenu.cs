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
        int TableId, int AreaId, string AreaName,
        decimal ServiceChargePercent, decimal ServiceChargeVatPercent,
        int? PriceTableId, string? PriceTableName, DateTime ResolvedAt,
        IReadOnlyList<CategoryDto> Categories,
        IReadOnlyList<MenuItemDto> Items);

    public sealed record CategoryDto(
        int CategoryId, string Code, string Name, int? ParentId,
        string Path, short DisplayOrder, int ItemCount);

    public sealed record MenuItemDto(
        int ItemId, string Code, string Name, string? Description, string? ImageUrl,
        int BaseUomId, string UomCode, string UomName,
        IReadOnlyList<int> CategoryIds, int? KitchenStationId, string? KitchenStationName,
        bool IsSetMenu, bool IsStockable, bool IsAvailable,
        decimal? RawPrice, bool IsVatIncluded, decimal VatPercent,
        decimal? BasePrice, decimal? DisplayPrice,
        SetMenuSpec? SetMenu);

    public sealed record SetMenuSpec(
        IReadOnlyList<SetMenuMainComponent> MainComponents,
        IReadOnlyList<ChoiceCategorySpec> ChoiceCategories);

    public sealed record SetMenuMainComponent(
        int ItemId, string ItemName, decimal Quantity, bool IsFixed,
        string UomCode, string UomName);

    public sealed record ChoiceCategorySpec(
        int ChoiceCategoryId, string Name, short MinChoice, short? MaxChoice,
        short DisplayOrder, IReadOnlyList<ModifierSpec> Modifiers);

    public sealed record ModifierSpec(
        int ModifierId, int ItemId, string Name, decimal ExtraPrice,
        int MinPerModifier, int MaxPerModifier, short DisplayOrder);

    internal sealed class Handler(IDbContext db, IDateTimeProvider clock, IRoundingConfig rc)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var table = await db.Tables
                .Where(t => t.Id == request.TableId)
                .Select(t => new
                {
                    t.Id, t.AreaId, AreaName = t.Area.Name,
                    t.Area.ServiceChargePercent, t.Area.ServiceChargeVatPercent,
                })
                .FirstOrDefaultAsync(ct);
            if (table is null) return Result.Failure<Response>(TableErrors.NotFound);

            var activePriceTable = await db.PriceTables
                .Where(p => p.IsActive)
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefaultAsync(ct);
            if (activePriceTable is null)
                return Result.Failure<Response>(PriceTableErrors.NoActivePriceTable);

            var now = clock.UtcNow;
            var time = TimeOnly.FromDateTime(now);
            var dayBit = 1 << (((int)now.DayOfWeek + 6) % 7); // Mon=bit0 … Sun=bit6

            // 1. Visible categories for this area (junction + subtree via Path prefix).
            var areaCategoryPaths = await db.AreaMenuCategories
                .Where(amc => amc.AreaId == table.AreaId)
                .Select(amc => amc.Category.Path)
                .ToListAsync(ct);

            var allCategories = await db.Categories
                .Where(c => c.IsActive)
                .Select(c => new { c.Id, c.Code, c.Name, c.ParentId, c.Path, c.DisplayOrder })
                .ToListAsync(ct);

            var visibleCategories = allCategories
                .Where(c => areaCategoryPaths.Any(p => c.Path.StartsWith(p)))
                .ToList();
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
                    i.Id, i.Code, i.Name, i.Description, i.ImageUrl, i.BaseUomId,
                    UomCode = i.BaseUom.Code, UomName = i.BaseUom.Name,
                    i.VatPercent, i.KitchenStationId,
                    KitchenStationName = i.KitchenStation != null ? i.KitchenStation.Name : null,
                    i.IsStockable, IsSetMenu = i.SetMenu != null,
                })
                .ToListAsync(ct);
            var realItemIds = items.Select(i => i.Id).ToList();

            // 3. Matching variants in the active price table, with their entries.
            var candidateVariants = await db.PriceVariants
                .Where(v => v.IsActive && v.PriceTable.IsActive
                    && v.PriceTableId == activePriceTable.Id
                    && (v.BeginTime == null || (v.BeginTime <= time && time < v.EndTime))
                    && (v.AppliesToAllAreas
                        || db.PriceVariantAreas.Any(pva => pva.PriceVariantId == v.Id && pva.AreaId == table.AreaId)))
                .Select(v => new
                {
                    v.Id, v.BeginTime, v.EndTime, v.DayMask, v.AppliesToAllAreas, v.CreatedAt,
                })
                .ToListAsync(ct);

            // Day-mask filter + specificity in memory (bitmask ops don't translate cleanly).
            var matching = candidateVariants
                .Where(v => v.DayMask == null || (v.DayMask.Value & dayBit) != 0)
                .Select(v => new
                {
                    v.Id,
                    Spec = (v.BeginTime != null || v.EndTime != null ? 1 : 0)
                         + (v.DayMask != null ? 1 : 0)
                         + (v.AppliesToAllAreas ? 0 : 1),
                    v.CreatedAt,
                })
                .ToList();
            var matchingIds = matching.Select(m => m.Id).ToList();

            var entries = await db.PriceEntries
                .Where(pe => matchingIds.Contains(pe.PriceVariantId) && realItemIds.Contains(pe.ItemId))
                .Select(pe => new { pe.PriceVariantId, pe.ItemId, pe.Price, pe.IsVatIncluded })
                .ToListAsync(ct);

            // Most-specific-wins per item.
            var specById = matching.ToDictionary(m => m.Id, m => (m.Spec, m.CreatedAt));
            var winnerByItem = entries
                .GroupBy(e => e.ItemId)
                .ToDictionary(g => g.Key, g => g
                    .OrderByDescending(e => specById[e.PriceVariantId].Spec)
                    .ThenByDescending(e => specById[e.PriceVariantId].CreatedAt)
                    .First());

            // Stock availability for stockable items.
            var stockByItem = await db.ItemStocks
                .Where(s => realItemIds.Contains(s.ItemId))
                .Select(s => new { s.ItemId, Available = s.CurrentQty - s.ReservedQty > 0 })
                .ToListAsync(ct);
            var stockAvail = stockByItem.ToDictionary(s => s.ItemId, s => s.Available);

            // Set-menu specs for set items.
            var setItemIds = items.Where(i => i.IsSetMenu).Select(i => i.Id).ToList();
            var setSpecs = await LoadSetMenuSpecsAsync(setItemIds, ct);

            var categoryIdsByItem = itemCategoryLinks
                .Where(l => realItemIds.Contains(l.ItemId))
                .GroupBy(l => l.ItemId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.CategoryId).ToList());

            var itemDtos = items.Select(i =>
            {
                winnerByItem.TryGetValue(i.Id, out var w);
                decimal? rawPrice = w?.Price;
                bool isVatIncluded = w?.IsVatIncluded ?? false;
                decimal? basePrice = null, displayPrice = null;
                if (rawPrice is { } price)
                {
                    var (b, d) = MenuPricing.ComputePrices(price, isVatIncluded, i.VatPercent, rc);
                    basePrice = b;
                    displayPrice = d;
                }

                bool available = !i.IsStockable || stockAvail.GetValueOrDefault(i.Id, true);

                return new MenuItemDto(
                    i.Id, i.Code, i.Name, i.Description, i.ImageUrl, i.BaseUomId,
                    i.UomCode, i.UomName,
                    categoryIdsByItem.GetValueOrDefault(i.Id) ?? new(),
                    i.KitchenStationId, i.KitchenStationName,
                    i.IsSetMenu, i.IsStockable, available,
                    rawPrice, isVatIncluded, i.VatPercent, basePrice, displayPrice,
                    i.IsSetMenu ? setSpecs.GetValueOrDefault(i.Id) : null);
            }).ToList();

            // itemCount per category (subtree) for UI badge.
            var itemCountByCategory = visibleCategories.ToDictionary(
                c => c.Id,
                c => itemCategoryLinks.Count(l => l.CategoryId == c.Id && realItemIds.Contains(l.ItemId)));

            var categoryDtos = visibleCategories
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new CategoryDto(
                    c.Id, c.Code, c.Name, c.ParentId, c.Path, c.DisplayOrder,
                    itemCountByCategory.GetValueOrDefault(c.Id)))
                .ToList();

            return Result.Success(new Response(
                table.Id, table.AreaId, table.AreaName,
                table.ServiceChargePercent, table.ServiceChargeVatPercent,
                activePriceTable.Id, activePriceTable.Name, now,
                categoryDtos, itemDtos));
        }

        private async Task<Dictionary<int, SetMenuSpec>> LoadSetMenuSpecsAsync(
            IReadOnlyList<int> setItemIds, CancellationToken ct)
        {
            if (setItemIds.Count == 0) return new();

            var details = await db.SetMenuDetails
                .Where(d => setItemIds.Contains(d.SetMenuItemId))
                .OrderBy(d => d.DisplayOrder)
                .Select(d => new
                {
                    d.SetMenuItemId, d.DetailType, d.ComponentItemId, d.ChoiceCategoryId,
                    d.Quantity, d.IsFixed, d.DisplayOrder,
                    ComponentName = d.ComponentItem != null ? d.ComponentItem.Name : null,
                    ComponentUomCode = d.ComponentItem != null ? d.ComponentItem.BaseUom.Code : null,
                    ComponentUomName = d.ComponentItem != null ? d.ComponentItem.BaseUom.Name : null,
                })
                .ToListAsync(ct);

            var choiceCategoryIds = details
                .Where(d => d.ChoiceCategoryId != null)
                .Select(d => d.ChoiceCategoryId!.Value).Distinct().ToList();

            var choiceCategories = await db.ChoiceCategories
                .Where(cc => choiceCategoryIds.Contains(cc.Id))
                .Select(cc => new { cc.Id, cc.Name, cc.MinChoice, cc.MaxChoice, cc.DisplayOrder })
                .ToListAsync(ct);

            var modifiers = await db.Modifiers
                .Where(m => choiceCategoryIds.Contains(m.ChoiceCategoryId) && m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new
                {
                    m.ChoiceCategoryId, ModifierId = m.ItemId, m.ItemId,
                    Name = m.Item.Name, m.ExtraPrice, m.MinPerModifier, m.MaxPerModifier, m.DisplayOrder,
                })
                .ToListAsync(ct);
            var modifiersByCc = modifiers.GroupBy(m => m.ChoiceCategoryId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new Dictionary<int, SetMenuSpec>();
            foreach (var setId in setItemIds)
            {
                var d = details.Where(x => x.SetMenuItemId == setId).ToList();

                var mains = d
                    .Where(x => x.DetailType == SetMenuDetailType.Component && x.ComponentItemId != null)
                    .Select(x => new SetMenuMainComponent(
                        x.ComponentItemId!.Value, x.ComponentName ?? "", x.Quantity ?? 0m,
                        x.IsFixed ?? false, x.ComponentUomCode ?? "", x.ComponentUomName ?? ""))
                    .ToList();

                var choices = d
                    .Where(x => x.DetailType == SetMenuDetailType.ChoiceCategory && x.ChoiceCategoryId != null)
                    .Select(x =>
                    {
                        var cc = choiceCategories.First(c => c.Id == x.ChoiceCategoryId!.Value);
                        var mods = (modifiersByCc.GetValueOrDefault(cc.Id) ?? new())
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
