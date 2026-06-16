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

            // LEFT JOIN ItemStock via subquery (Item has no nav to ItemStock).
            var projected = q.Select(i => new StockableItem(
                i.Id,
                i.Code,
                i.Name,
                i.BaseUomId,
                i.BaseUom.Code,
                i.BaseUom.Name,
                dbContext.ItemStocks.Where(s => s.ItemId == i.Id).Select(s => (decimal?)s.CurrentQty).FirstOrDefault() ?? 0m,
                i.LowStockThreshold,
                dbContext.ItemStocks.Where(s => s.ItemId == i.Id).Select(s => s.LastMovementAt).FirstOrDefault()));

            if (request.LowStock == true)
            {
                projected = projected.Where(x => x.LowStockThreshold != null && x.CurrentQty <= x.LowStockThreshold);
            }

            List<StockableItem> rows = await projected.OrderBy(x => x.Name).ToListAsync(ct);
            return Result.Success<IReadOnlyList<StockableItem>>(rows);
        }
    }
}
