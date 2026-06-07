using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Pricing;

namespace Rpom.Infrastructure.Pricing;

/// <summary>
///     Price table in effect by date + most-specific variant per item (time window, day mask,
///     area scope). Extracted from GetMenu so the cashier write flow resolves the same price.
/// </summary>
internal sealed class MenuPriceResolver(IDbContext db) : IMenuPriceResolver
{
    public async Task<MenuPriceResolution> ResolveAsync(
        int areaId, DateTime at, IReadOnlyCollection<int> itemIds, CancellationToken ct)
    {
        var time = TimeOnly.FromDateTime(at);
        var today = DateOnly.FromDateTime(at);
        int dayBit = 1 << (((int)at.DayOfWeek + 6) % 7); // Mon=bit0 … Sun=bit6

        // Active price table currently IN EFFECT (date window). Tie-break: a dated table
        // beats an open-ended one, newest BeginDate, then newest row.
        var priceTable = await db.PriceTables
            .Where(p => p.IsActive
                        && (p.BeginDate == null || p.BeginDate <= today)
                        && (p.EndDate == null || today <= p.EndDate))
            .OrderByDescending(p => p.BeginDate.HasValue)
            .ThenByDescending(p => p.BeginDate)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(ct);

        if (priceTable is null || itemIds.Count == 0)
        {
            return new MenuPriceResolution(
                priceTable?.Id, priceTable?.Name,
                new Dictionary<int, ResolvedPrice>());
        }

        // Candidate variants: active, in this table, matching time window + area.
        var candidateVariants = await db.PriceVariants
            .Where(v => v.IsActive && v.PriceTable.IsActive
                                   && v.PriceTableId == priceTable.Id
                                   && (v.BeginTime == null || v.BeginTime <= time)
                                   && (v.EndTime == null || time < v.EndTime)
                                   && (v.AppliesToAllAreas
                                       || db.PriceVariantAreas.Any(pva =>
                                           pva.PriceVariantId == v.Id && pva.AreaId == areaId)))
            .Select(v => new { v.Id, v.BeginTime, v.EndTime, v.DayMask, v.AppliesToAllAreas, v.CreatedAt })
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
                v.CreatedAt
            })
            .ToList();
        var matchingIds = matching.Select(m => m.Id).ToList();

        var entries = await db.PriceEntries
            .Where(pe => matchingIds.Contains(pe.PriceVariantId) && itemIds.Contains(pe.ItemId))
            .Select(pe => new { pe.PriceVariantId, pe.ItemId, pe.Price, pe.IsVatIncluded })
            .ToListAsync(ct);

        var specById = matching.ToDictionary(m => m.Id, m => (m.Spec, m.CreatedAt));
        var prices = entries
            .GroupBy(e => e.ItemId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var w = g
                        .OrderByDescending(e => specById[e.PriceVariantId].Spec)
                        .ThenByDescending(e => specById[e.PriceVariantId].CreatedAt)
                        .First();
                    return new ResolvedPrice(w.Price, w.IsVatIncluded);
                });

        return new MenuPriceResolution(priceTable.Id, priceTable.Name, prices);
    }
}
