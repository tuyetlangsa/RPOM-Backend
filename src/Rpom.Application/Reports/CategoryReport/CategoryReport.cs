using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Reports;
using Rpom.Domain.Common;

namespace Rpom.Application.Reports.CategoryReport;

public static class CategoryReport
{
    public sealed record Query(ReportFilter Filter) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int CategoryId, string CategoryName,
        string? ParentCategoryName,
        decimal TotalQuantity, decimal TotalRevenue, decimal TotalDiscount);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query q, CancellationToken ct)
        {
            var from = q.Filter.FromDate ?? DateTime.UtcNow.Date.AddDays(-30);
            var to = q.Filter.ToDate ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            // Get all invoice lines in period
            var lineQuery = db.TicketInvoiceLines
                .Include(l => l.TicketInvoice)
                .Where(l => l.TicketInvoice.ClosedAt >= from && l.TicketInvoice.ClosedAt <= to);

            if (q.Filter.CounterId.HasValue)
                lineQuery = lineQuery.Where(l => l.TicketInvoice.CounterId == q.Filter.CounterId.Value);
            if (q.Filter.AreaId.HasValue)
                lineQuery = lineQuery.Where(l => l.TicketInvoice.AreaId == q.Filter.AreaId.Value);

            var lines = await lineQuery.ToListAsync(ct);
            var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();

            // Get item → category mapping
            var itemCategories = itemIds.Count > 0
                ? await db.ItemCategories
                    .Where(ic => itemIds.Contains(ic.ItemId))
                    .Include(ic => ic.Category)
                    .ToListAsync(ct)
                : [];

            var catLookup = itemCategories
                .GroupBy(ic => ic.ItemId)
                .ToDictionary(g => g.Key, g => g.First().Category);

            // Aggregate by category
            var grouped = lines
                .GroupBy(l => catLookup.GetValueOrDefault(l.ItemId)?.Id ?? 0)
                .Select(g =>
                {
                    var cat = catLookup.Values.FirstOrDefault(c => c.Id == g.Key);
                    return new Response(
                        g.Key, cat?.Name ?? "Uncategorized", cat?.Parent?.Name,
                        g.Sum(l => l.Quantity), g.Sum(l => l.TotalAmount), g.Sum(l => l.TotalDiscount));
                })
                .OrderByDescending(r => r.TotalRevenue)
                .ToList();

            return Result.Success<IReadOnlyList<Response>>(grouped);
        }
    }
}
