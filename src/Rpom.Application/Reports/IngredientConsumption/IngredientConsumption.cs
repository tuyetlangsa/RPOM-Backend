using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Reports;
using Rpom.Domain.Common;

namespace Rpom.Application.Reports.IngredientConsumption;

public static class IngredientConsumption
{
    public sealed record Query(ReportFilter Filter, int? ItemId = null) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int IngredientItemId, string IngredientCode, string IngredientName,
        string BaseUomCode, decimal TotalConsumedQty,
        decimal CurrentStock, decimal PercentUsed);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query q, CancellationToken ct)
        {
            var from = q.Filter.FromDate ?? DateTime.UtcNow.Date.AddDays(-30);
            var to = q.Filter.ToDate ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            // Get all sold items in period
            var lineQuery = db.TicketInvoiceLines
                .Where(l => l.TicketInvoice.ClosedAt >= from && l.TicketInvoice.ClosedAt <= to);

            if (q.Filter.CounterId.HasValue)
                lineQuery = lineQuery.Where(l => l.TicketInvoice.CounterId == q.Filter.CounterId.Value);

            var soldItems = await lineQuery
                .GroupBy(l => l.ItemId)
                .Select(g => new { ItemId = g.Key, TotalQty = g.Sum(l => l.Quantity) })
                .ToListAsync(ct);

            var soldItemIds = soldItems.Select(s => s.ItemId).ToList();

            // Get BOM lines for those items
            var bomLines = soldItemIds.Count > 0
                ? await db.BomLines
                    .Where(b => soldItemIds.Contains(b.SellableItemId))
                    .ToListAsync(ct)
                : [];

            // Get ingredient stock levels
            var ingredientIds = bomLines.Select(b => b.MaterialItemId).Distinct().ToList();
            var stocks = ingredientIds.Count > 0
                ? await db.ItemStocks
                    .Where(s => ingredientIds.Contains(s.ItemId))
                    .ToListAsync(ct)
                : [];
            var stockDict = stocks.ToDictionary(s => s.ItemId);

            // Calculate consumption
            var soldQtyDict = soldItems.ToDictionary(s => s.ItemId, s => s.TotalQty);
            var consumption = new Dictionary<int, decimal>();

            foreach (var bom in bomLines)
            {
                if (!soldQtyDict.TryGetValue(bom.SellableItemId, out var soldQty)) continue;
                decimal consumed = soldQty * bom.Quantity;
                if (consumption.ContainsKey(bom.MaterialItemId))
                    consumption[bom.MaterialItemId] += consumed;
                else
                    consumption[bom.MaterialItemId] = consumed;
            }

            // Get ingredient item info
            var ingredientItems = consumption.Keys.Count > 0
                ? await db.Items
                    .Where(i => consumption.Keys.Contains(i.Id))
                    .Include(i => i.BaseUom)
                    .ToListAsync(ct)
                : [];

            if (q.ItemId.HasValue)
                ingredientItems = ingredientItems.Where(i => i.Id == q.ItemId.Value).ToList();

            var result = ingredientItems.Select(i =>
            {
                decimal consumed = consumption.GetValueOrDefault(i.Id);
                decimal currentStock = stockDict.TryGetValue(i.Id, out var st) ? st.CurrentQty : 0;
                decimal remaining = currentStock + consumed;
                return new Response(i.Id, i.Code, i.Name, i.BaseUom.Code,
                    consumed, currentStock,
                    remaining > 0 ? consumed / remaining * 100 : 100);
            }).OrderByDescending(r => r.TotalConsumedQty).ToList();

            return Result.Success<IReadOnlyList<Response>>(result);
        }
    }
}
