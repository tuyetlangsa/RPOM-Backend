using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Lookups.GetStockableItems;

/// <summary>
///     All active stockable items (IsStockable=true) with their current stock — usable across
///     pages: stock-movement forms, BOM material dropdowns, and the stock dashboard. Items never
///     stocked yet (no ItemStock row) appear with CurrentQty = 0 / LastMovementAt = null.
/// </summary>
public static class GetStockableItems
{
    public sealed record Query(string? Search, bool? LowStock) : IQuery<IReadOnlyList<StockableItem>>;

    public sealed record StockableItem(
        int ItemId,
        string Code,
        string Name,
        int BaseUomId,
        string BaseUomCode,
        string BaseUomName,
        decimal CurrentQty,
        decimal? LowStockThreshold,
        DateTime? LastMovementAt);

    internal sealed class Handler(IDbContext dbContext)
        : IQueryHandler<Query, IReadOnlyList<StockableItem>>
    {
        public async Task<Result<IReadOnlyList<StockableItem>>> Handle(Query request, CancellationToken ct)
        {
            var q = dbContext.Items.Where(i => i.IsStockable && i.IsActive);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                string s = request.Search.Trim().ToLower();
                q = q.Where(i => i.Code.ToLower().Contains(s) || i.Name.ToLower().Contains(s));
            }

            var items = await q
                .OrderBy(i => i.Name)
                .Select(i => new
                {
                    i.Id,
                    i.Code,
                    i.Name,
                    i.BaseUomId,
                    BaseUomCode = i.BaseUom.Code,
                    BaseUomName = i.BaseUom.Name,
                    i.LowStockThreshold
                })
                .ToListAsync(ct);

            var ids = items.Select(i => i.Id).ToList();
            var stockByItem = (await dbContext.ItemStocks
                    .Where(s => ids.Contains(s.ItemId))
                    .Select(s => new { s.ItemId, s.CurrentQty, s.LastMovementAt })
                    .ToListAsync(ct))
                .ToDictionary(s => s.ItemId);

            IEnumerable<StockableItem> rows = items.Select(i =>
            {
                stockByItem.TryGetValue(i.Id, out var st);
                return new StockableItem(
                    i.Id, i.Code, i.Name, i.BaseUomId, i.BaseUomCode, i.BaseUomName,
                    st?.CurrentQty ?? 0m, i.LowStockThreshold, st?.LastMovementAt);
            });

            if (request.LowStock == true)
            {
                rows = rows.Where(x => x.LowStockThreshold != null && x.CurrentQty <= x.LowStockThreshold);
            }

            return Result.Success<IReadOnlyList<StockableItem>>(rows.ToList());
        }
    }
}
