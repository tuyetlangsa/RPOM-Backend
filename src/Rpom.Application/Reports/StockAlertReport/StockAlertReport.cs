using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Reports.StockAlertReport;

public static class StockAlertReport
{
    public sealed record Query(string? Search, bool? LowStock) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int ItemId, string ItemCode, string ItemName,
        string BaseUomCode, string BaseUomName,
        decimal CurrentQty, decimal? LowStockThreshold,
        string Status, DateTime? LastMovementAt);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query q, CancellationToken ct)
        {
            var query = db.Items.Where(i => i.IsStockable && i.IsActive);

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var s = q.Search.Trim().ToLower();
                query = query.Where(i => i.Code.ToLower().Contains(s) || i.Name.ToLower().Contains(s));
            }

            var items = await query.Select(i => new
            {
                i.Id, i.Code, i.Name,
                BaseUomCode = i.BaseUom.Code,
                BaseUomName = i.BaseUom.Name,
                i.LowStockThreshold
            }).ToListAsync(ct);

            var itemIds = items.Select(i => i.Id).ToList();
            var stockDict = itemIds.Count > 0
                ? (await db.ItemStocks.Where(s => itemIds.Contains(s.ItemId)).ToListAsync(ct))
                .ToDictionary(s => s.ItemId)
                : new Dictionary<int, Domain.Inventory.ItemStock>();

            var rows = items.Select(i =>
            {
                stockDict.TryGetValue(i.Id, out var st);
                decimal currentQty = st?.CurrentQty ?? 0m;
                string status = i.LowStockThreshold.HasValue
                    ? currentQty <= 0 ? "NEGATIVE"
                    : currentQty <= i.LowStockThreshold.Value ? "LOW" : "OK"
                    : "OK";
                return new Response(i.Id, i.Code, i.Name,
                    i.BaseUomCode, i.BaseUomName, currentQty,
                    i.LowStockThreshold, status, st?.LastMovementAt);
            });

            if (q.LowStock == true)
                rows = rows.Where(r => r.Status == "LOW" || r.Status == "NEGATIVE");

            return Result.Success<IReadOnlyList<Response>>(rows.OrderBy(r => r.ItemName).ToList());
        }
    }
}
