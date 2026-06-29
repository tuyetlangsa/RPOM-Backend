using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Reports;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Reports.DetailedRevenueReport;

public static class DetailedRevenueReport
{
    public sealed record Query(ReportFilter Filter, int PageNumber = 1, int PageSize = 50) : IQuery<Response>;

    public sealed record Response(
        IReadOnlyList<BillRow> Bills, SummaryRow Summary,
        int PageNumber, int PageSize, int TotalCount);

    public sealed record BillRow(
        int RowNumber, long TicketId, string TicketCode,
        DateTime ClosedAt, string TableCode, string AreaName, string CounterName,
        string ShiftName, string? WaiterName, string? ClosedByName,
        short GuestCount, int ItemCount,
        decimal Subtotal, decimal DiscountAmount, decimal ServiceChargeAmount,
        decimal VatAmount, decimal TotalAmount, decimal PaidAmount,
        decimal RefundAmount, string Status, string? CancellationReason,
        double DurationMinutes, string PaymentMethods);

    public sealed record SummaryRow(
        int TotalBills, decimal TotalRevenue, decimal TotalDiscount,
        decimal TotalPaid, decimal AverageBill);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query q, CancellationToken ct)
        {
            var from = q.Filter.FromDate ?? DateTime.UtcNow.Date.AddDays(-30);
            var to = q.Filter.ToDate ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            var query = db.TicketInvoices
                .Include(i => i.Lines)
                .Where(i => i.ClosedAt >= from && i.ClosedAt <= to);

            if (q.Filter.CounterId.HasValue)
                query = query.Where(i => i.CounterId == q.Filter.CounterId.Value);
            if (q.Filter.AreaId.HasValue)
                query = query.Where(i => i.AreaId == q.Filter.AreaId.Value);
            if (q.Filter.ShiftId.HasValue)
                query = query.Where(i => i.ShiftId == q.Filter.ShiftId.Value);

            int totalCount = await query.CountAsync(ct);

            var invoices = await query
                .OrderByDescending(i => i.ClosedAt)
                .Skip((q.PageNumber - 1) * q.PageSize)
                .Take(q.PageSize)
                .ToListAsync(ct);

            var ticketIds = invoices.Select(i => i.TicketId).ToList();
            var payByTicket = ticketIds.Count > 0
                ? (await db.TicketPaymentDetails
                    .Where(p => ticketIds.Contains(p.TicketId) && p.Status == TicketPaymentStatus.Success)
                    .Include(p => p.PaymentMethod)
                    .ToListAsync(ct))
                .GroupBy(p => p.TicketId).ToDictionary(g => g.Key, g => g.ToList())
                : [];

            int rowNum = (q.PageNumber - 1) * q.PageSize;
            var bills = invoices.Select(i =>
            {
                rowNum++;
                var pays = payByTicket.GetValueOrDefault(i.TicketId, []);
                return new BillRow(
                    rowNum, i.TicketId, i.TicketCode, i.ClosedAt,
                    i.TableCode, "", "", "",
                    i.WaiterName, i.ClosedByName,
                    i.GuestCount, i.Lines.Count,
                    i.Subtotal, i.DiscountAmount, i.ServiceChargeAmount,
                    i.VatAmount, i.TotalAmount, i.PaidAmount,
                    i.RefundAmount, "CLOSED", null,
                    (i.ClosedAt - i.OpenedAt).TotalMinutes,
                    string.Join(" + ", pays.Select(p => p.PaymentMethod.Code).Distinct()));
            }).ToList();

            var summary = new SummaryRow(
                totalCount, invoices.Sum(i => i.TotalAmount),
                invoices.Sum(i => i.DiscountAmount), invoices.Sum(i => i.PaidAmount),
                totalCount > 0 ? invoices.Sum(i => i.TotalAmount) / totalCount : 0);

            return Result.Success(new Response(bills, summary, q.PageNumber, q.PageSize, totalCount));
        }
    }
}
