using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Inventory.GetStockDashboard;

public static class GetStockDashboard
{
    public sealed record Query(string? Search, bool? LowStock) : IQuery<IReadOnlyList<StockItem>>;

    public sealed record StockItem(
        int ItemId,
        string ItemCode,
        string ItemName,
        string BaseUomCode,
        string BaseUomName,
        decimal CurrentQty,
        decimal ReservedQty,
        decimal? LowStockThreshold,
        DateTime? LastMovementAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<StockItem>>
    {
        public async Task<Result<IReadOnlyList<StockItem>>> Handle(Query request, CancellationToken ct)
        {
            // Drive from Items (LEFT JOIN ItemStock) so EVERY stockable item shows — including ones
            // never stocked yet (CurrentQty = 0), which are exactly the ones needing a restock.
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
                    BaseUomCode = i.BaseUom.Code,
                    BaseUomName = i.BaseUom.Name,
                    i.LowStockThreshold
                })
                .ToListAsync(ct);

            var ids = items.Select(i => i.Id).ToList();
            var stockByItem = (await dbContext.ItemStocks
                    .Where(s => ids.Contains(s.ItemId))
                    .Select(s => new { s.ItemId, s.CurrentQty, s.ReservedQty, s.LastMovementAt, s.UpdatedAt })
                    .ToListAsync(ct))
                .ToDictionary(s => s.ItemId);

            IEnumerable<StockItem> rows = items.Select(i =>
            {
                stockByItem.TryGetValue(i.Id, out var st);
                return new StockItem(
                    i.Id, i.Code, i.Name, i.BaseUomCode, i.BaseUomName,
                    st?.CurrentQty ?? 0m,
                    st?.ReservedQty ?? 0m,
                    i.LowStockThreshold,
                    st?.LastMovementAt,
                    st?.UpdatedAt ?? default);
            });

            if (request.LowStock == true)
            {
                rows = rows.Where(x => x.LowStockThreshold != null && x.CurrentQty <= x.LowStockThreshold);
            }

            return Result.Success<IReadOnlyList<StockItem>>(rows.ToList());
        }
    }
}
