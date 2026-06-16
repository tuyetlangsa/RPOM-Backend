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
            IQueryable<Domain.Inventory.ItemStock> q = dbContext.ItemStocks.AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                string s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Item.Code.ToLower().Contains(s)
                              || x.Item.Name.ToLower().Contains(s));
            }

            if (request.LowStock == true)
            {
                q = q.Where(x => x.Item.LowStockThreshold != null
                              && x.CurrentQty <= x.Item.LowStockThreshold);
            }

            var rows = await q
                .OrderBy(x => x.Item.Name)
                .Select(x => new StockItem(
                    x.ItemId,
                    x.Item.Code,
                    x.Item.Name,
                    x.Item.BaseUom.Code,
                    x.Item.BaseUom.Name,
                    x.CurrentQty,
                    x.ReservedQty,
                    x.Item.LowStockThreshold,
                    x.LastMovementAt,
                    x.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<StockItem>>(rows);
        }
    }
}
