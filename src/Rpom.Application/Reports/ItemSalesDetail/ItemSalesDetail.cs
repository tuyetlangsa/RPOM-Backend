using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Reports;
using Rpom.Domain.Common;

namespace Rpom.Application.Reports.ItemSalesDetail;

public static class ItemSalesDetail
{
    public sealed record Query(ReportFilter Filter, long? TicketId = null,
        int PageNumber = 1, int PageSize = 20) : IQuery<Response>;

    public sealed record Response(
        IReadOnlyList<BillWithItems> Bills, SummaryRow Summary,
        int PageNumber, int PageSize, int TotalCount);

    public sealed record BillWithItems(
        int RowNumber, long TicketId, string TicketCode,
        DateTime ClosedAt, string TableCode, string AreaName,
        string? WaiterName, short GuestCount,
        decimal Subtotal, decimal DiscountAmount,
        decimal ServiceChargeAmount, decimal TotalAmount,
        decimal PaidAmount, string Status,
        IReadOnlyList<BillItemRow> Items);

    public sealed record BillItemRow(
        int RowNumber, string ItemCode, string ItemName,
        string UomCode, decimal Quantity, decimal UnitPrice,
        decimal ChoicePricePerUnit, decimal VatPercent,
        decimal LineSubtotal, decimal DiscountAmount,
        decimal TotalAmount, string? Modifiers);

    public sealed record SummaryRow(
        int TotalBills, int TotalItems, decimal TotalQuantity,
        decimal TotalRevenue, decimal TotalDiscount, decimal TotalPaid);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query q, CancellationToken ct)
        {
            var from = q.Filter.FromDate ?? DateTime.UtcNow.Date.AddDays(-30);
            var to = q.Filter.ToDate ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            var query = db.TicketInvoices
                .Include(i => i.Lines).AsQueryable();

            if (q.TicketId.HasValue)
                query = query.Where(i => i.TicketId == q.TicketId.Value);
            else
            {
                query = query.Where(i => i.ClosedAt >= from && i.ClosedAt <= to);
                if (q.Filter.CounterId.HasValue)
                    query = query.Where(i => i.CounterId == q.Filter.CounterId.Value);
                if (q.Filter.AreaId.HasValue)
                    query = query.Where(i => i.AreaId == q.Filter.AreaId.Value);
            }

            int totalCount = await query.CountAsync(ct);

            var invoices = await query
                .OrderByDescending(i => i.ClosedAt)
                .Skip((q.PageNumber - 1) * q.PageSize)
                .Take(q.PageSize)
                .ToListAsync(ct);

            int rowNum = (q.PageNumber - 1) * q.PageSize;
            var bills = invoices.Select(i =>
            {
                rowNum++;
                return new BillWithItems(
                    rowNum, i.TicketId, i.TicketCode, i.ClosedAt,
                    i.TableCode, "", i.WaiterName, i.GuestCount,
                    i.Subtotal, i.DiscountAmount, i.ServiceChargeAmount,
                    i.TotalAmount, i.PaidAmount, "CLOSED",
                    i.Lines.OrderBy(l => l.DisplayOrder).Select((l, idx) => new BillItemRow(
                        idx + 1, l.ItemCode, l.ItemName, l.UomCode,
                        l.Quantity, l.UnitPrice, l.ChoicePricePerUnit,
                        l.VatPercent, l.LineSubtotal, l.TotalDiscount,
                        l.TotalAmount, null)).ToList());
            }).ToList();

            var summary = new SummaryRow(
                totalCount, invoices.Sum(i => i.Lines.Count),
                invoices.Sum(i => i.Lines.Sum(l => l.Quantity)),
                invoices.Sum(i => i.TotalAmount),
                invoices.Sum(i => i.DiscountAmount),
                invoices.Sum(i => i.PaidAmount));

            return Result.Success(new Response(bills, summary, q.PageNumber, q.PageSize, totalCount));
        }
    }
}
