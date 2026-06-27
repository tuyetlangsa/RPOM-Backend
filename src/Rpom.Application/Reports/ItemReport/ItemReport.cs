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

            var lineQuery = db.TicketInvoiceLines
                .Where(l => l.TicketInvoice.ClosedAt >= from && l.TicketInvoice.ClosedAt <= to);

            if (q.Filter.CounterId.HasValue)
                lineQuery = lineQuery.Where(l => l.TicketInvoice.CounterId == q.Filter.CounterId.Value);
            if (q.Filter.AreaId.HasValue)
                lineQuery = lineQuery.Where(l => l.TicketInvoice.AreaId == q.Filter.AreaId.Value);
            if (q.Filter.CategoryId.HasValue)
                lineQuery = lineQuery.Where(l =>
                    l.TicketInvoice.Lines.Any(li => li.ItemId == l.ItemId)); // simplified

            var items = await lineQuery
                .GroupBy(l => new { l.ItemId, l.ItemCode, l.ItemName, l.UomCode })
                .Select(g => new Response(
                    g.Key.ItemId, g.Key.ItemCode, g.Key.ItemName, g.Key.UomCode,
                    g.Sum(l => l.Quantity),
                    g.Sum(l => l.TotalAmount),
                    g.Sum(l => l.TotalDiscount),
                    g.Select(l => l.TicketInvoiceId).Distinct().Count()))
                .OrderByDescending(r => r.TotalRevenue)
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(items);
        }
    }
}
