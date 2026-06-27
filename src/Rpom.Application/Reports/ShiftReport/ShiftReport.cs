using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Reports;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;

namespace Rpom.Application.Reports.ShiftReport;

public static class ShiftReport
{
    public sealed record Query(ReportFilter Filter) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        long CashDrawerSessionId,
        int CounterId, string CounterName,
        int ShiftId, string ShiftName,
        string? OpenedByStaffName,
        string? ClosedByStaffName,
        DateTime OpenedAt,
        DateTime? ClosedAt,
        decimal OpeningCash,
        decimal? ExpectedClosingCash,
        decimal? ActualClosingCash,
        decimal? Variance,
        int TotalBills,
        decimal TotalRevenue,
        decimal CashRevenue,
        decimal QrRevenue);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query q, CancellationToken ct)
        {
            var from = q.Filter.FromDate ?? DateTime.UtcNow.Date.AddDays(-7);
            var to = q.Filter.ToDate ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            var query = db.CashDrawerSessions
                .Include(d => d.Counter)
                .Include(d => d.Shift)
                .Where(d => d.Status == CashDrawerStatus.Closed && d.ClosedAt >= from && d.ClosedAt <= to);

            if (q.Filter.CounterId.HasValue)
                query = query.Where(d => d.CounterId == q.Filter.CounterId.Value);
            if (q.Filter.ShiftId.HasValue)
                query = query.Where(d => d.ShiftId == q.Filter.ShiftId.Value);

            var sessions = await query.OrderByDescending(d => d.ClosedAt).ToListAsync(ct);

            var result = new List<Response>();
            foreach (var s in sessions)
            {
                var tickets = await db.TicketInvoices
                    .Where(t => t.CounterId == s.CounterId
                        && t.ClosedAt >= s.OpenedAt
                        && t.ClosedAt <= (s.ClosedAt ?? DateTime.MaxValue))
                    .ToListAsync(ct);

                var ticketIds = tickets.Select(t => t.TicketId).ToList();
                var payments = ticketIds.Count > 0
                    ? await db.TicketPaymentDetails
                        .Where(p => ticketIds.Contains(p.TicketId) && p.Status == TicketPaymentStatus.Success)
                        .ToListAsync(ct)
                    : [];

                result.Add(new Response(
                    s.Id, s.CounterId, s.Counter.Name,
                    s.ShiftId, s.Shift.Name,
                    s.OpenedByStaff?.FullName,
                    s.ClosedByStaff?.FullName,
                    s.OpenedAt, s.ClosedAt,
                    s.OpeningCash, s.ExpectedClosingCash,
                    s.ActualClosingCash, s.Variance,
                    tickets.Count, tickets.Sum(t => t.TotalAmount),
                    payments.Where(p => p.PaymentMethod.Code == "CASH").Sum(p => p.Amount),
                    payments.Where(p => p.PaymentMethod.Code == "QR").Sum(p => p.Amount)));
            }

            return Result.Success<IReadOnlyList<Response>>(result);
        }
    }
}
