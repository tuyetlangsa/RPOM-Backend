using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Reports;
using Rpom.Domain.Common;

namespace Rpom.Application.Reports.ItemReport;

public static class ItemReport
{
    public sealed record Query(ReportFilter Filter) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int ItemId, string ItemCode, string ItemName,
        string UomCode, decimal TotalQuantity,
        decimal TotalRevenue, decimal TotalDiscount,
        int BillCount);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query q, CancellationToken ct)
        {
            var from = q.Filter.FromDate ?? DateTime.UtcNow.Date.AddDays(-30);
            var to = q.Filter.ToDate ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            var query = db.TicketInvoiceLines
                .Join(db.TicketInvoices,
                    l => l.TicketInvoiceId, i => i.Id,
                    (l, i) => new { l, i.ClosedAt, i.CounterId, i.AreaId })
                .Where(x => x.ClosedAt >= from && x.ClosedAt <= to);

            if (q.Filter.CounterId.HasValue)
                query = query.Where(x => x.CounterId == q.Filter.CounterId.Value);
            if (q.Filter.AreaId.HasValue)
                query = query.Where(x => x.AreaId == q.Filter.AreaId.Value);

            var raw = await query
                .Select(x => new { x.l.ItemId, x.l.ItemCode, x.l.ItemName, x.l.UomCode, x.l.Quantity, x.l.TotalAmount, x.l.TotalDiscount, x.l.TicketInvoiceId })
                .ToListAsync(ct);

            var items = raw
                .GroupBy(x => new { x.ItemId, x.ItemCode, x.ItemName, x.UomCode })
                .Select(g => new Response(
                    g.Key.ItemId, g.Key.ItemCode, g.Key.ItemName, g.Key.UomCode,
                    g.Sum(x => x.Quantity),
                    g.Sum(x => x.TotalAmount),
                    g.Sum(x => x.TotalDiscount),
                    g.Select(x => x.TicketInvoiceId).Distinct().Count()))
                .OrderByDescending(r => r.TotalRevenue)
                .ToList();

            return Result.Success<IReadOnlyList<Response>>(items);
        }
    }
}
