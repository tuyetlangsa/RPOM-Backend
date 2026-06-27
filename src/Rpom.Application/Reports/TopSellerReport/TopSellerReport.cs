using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Reports;
using Rpom.Domain.Common;

namespace Rpom.Application.Reports.TopSellerReport;

public static class TopSellerReport
{
    public sealed record Query(ReportFilter Filter, int TopN = 10,
        string By = "revenue") : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Rank, int ItemId, string ItemCode, string ItemName,
        decimal TotalQuantity, decimal TotalRevenue,
        decimal PercentageOfTotal);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query q, CancellationToken ct)
        {
            var from = q.Filter.FromDate ?? DateTime.UtcNow.Date.AddDays(-30);
            var to = q.Filter.ToDate ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            var lineQuery = db.TicketInvoiceLines
                .Where(l => l.TicketInvoice.ClosedAt >= from && l.TicketInvoice.ClosedAt <= to);

            if (q.Filter.CounterId.HasValue)
                lineQuery = lineQuery.Where(l => l.TicketInvoice.CounterId == q.Filter.CounterId.Value);
            if (q.Filter.AreaId.HasValue)
                lineQuery = lineQuery.Where(l => l.TicketInvoice.AreaId == q.Filter.AreaId.Value);

            var items = await lineQuery
                .GroupBy(l => new { l.ItemId, l.ItemCode, l.ItemName })
                .Select(g => new
                {
                    g.Key.ItemId, g.Key.ItemCode, g.Key.ItemName,
                    TotalQty = g.Sum(l => l.Quantity),
                    TotalRev = g.Sum(l => l.TotalAmount)
                }).ToListAsync(ct);

            decimal grandTotal = items.Sum(x => x.TotalRev);

            var ranked = (q.By == "qty"
                    ? items.OrderByDescending(x => x.TotalQty)
                    : items.OrderByDescending(x => x.TotalRev))
                .Take(q.TopN)
                .Select((x, idx) => new Response(
                    idx + 1, x.ItemId, x.ItemCode, x.ItemName,
                    x.TotalQty, x.TotalRev,
                    grandTotal > 0 ? x.TotalRev / grandTotal * 100 : 0))
                .ToList();

            return Result.Success<IReadOnlyList<Response>>(ranked);
        }
    }
}
