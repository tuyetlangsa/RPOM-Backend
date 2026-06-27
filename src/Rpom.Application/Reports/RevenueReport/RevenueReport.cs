using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Reports;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Reports.RevenueReport;

public static class RevenueReport
{
    public sealed record Query(ReportFilter Filter, string? GroupBy = "day") : IQuery<Response>;

    public sealed record Response(
        // A. Revenue
        decimal TotalRevenue, decimal TotalSubtotal, decimal TotalDiscount,
        decimal TotalServiceCharge, decimal TotalVat, decimal TotalRoundingAdjustment,
        decimal AverageBill, decimal RevenuePerGuest, decimal RevenuePerHour,
        // B. Volume
        int BillCount, int TotalGuests, decimal TotalItemsSold,
        decimal AverageItemsPerBill, decimal AverageGuestsPerBill,
        // C. Discount
        int DiscountedBillCount, decimal DiscountRate,
        decimal AverageDiscountPerDiscountedBill, decimal TotalDiscountPercent,
        // D. Payment
        decimal CashAmount, decimal QrAmount, decimal CashRate, decimal QrRate,
        decimal TotalPaidAmount, decimal AveragePaymentCountPerBill,
        decimal PaidVsRevenueDelta,
        // E. Operational
        double AverageServiceDurationMinutes, int CancelledItemCount,
        decimal CancellationRate, decimal RefundAmount,
        int OpenTicketCount, int CancelledTicketCount,
        // F. Comparison
        decimal PrevPeriodRevenue, decimal PrevPeriodChangePct,
        decimal SameDowLastWeekRevenue, decimal SameDowChangePct,
        decimal ThirtyDayAvgRevenue, decimal VsThirtyDayAvgPct,
        // Breakdown
        IReadOnlyList<BreakdownRow> Breakdown);

    public sealed record BreakdownRow(
        string Label,
        decimal TotalRevenue, decimal TotalSubtotal, decimal TotalDiscount,
        decimal TotalServiceCharge, decimal TotalVat,
        decimal AverageBill, int BillCount, int TotalGuests,
        decimal CashAmount, decimal QrAmount, decimal TotalPaidAmount,
        decimal AverageServiceDurationMinutes);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query q, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var from = q.Filter.FromDate ?? now.Date.AddDays(-30);
            var to = q.Filter.ToDate ?? now.Date.AddDays(1).AddTicks(-1);

            // --- Base invoice query ---
            var invoiceQuery = db.TicketInvoices
                .Where(i => i.ClosedAt >= from && i.ClosedAt <= to);
            if (q.Filter.CounterId.HasValue)
                invoiceQuery = invoiceQuery.Where(i => i.CounterId == q.Filter.CounterId.Value);
            if (q.Filter.AreaId.HasValue)
                invoiceQuery = invoiceQuery.Where(i => i.AreaId == q.Filter.AreaId.Value);
            if (q.Filter.ShiftId.HasValue)
                invoiceQuery = invoiceQuery.Where(i => i.ShiftId == q.Filter.ShiftId.Value);

            var invoices = await invoiceQuery.ToListAsync(ct);

            // --- Payment aggregation ---
            var ticketIds = invoices.Select(i => i.TicketId).ToList();
            var payments = ticketIds.Count > 0
                ? await db.TicketPaymentDetails
                    .Where(p => ticketIds.Contains(p.TicketId) && p.Status == TicketPaymentStatus.Success)
                    .ToListAsync(ct)
                : [];

            // --- Item count from lines ---
            var invoiceIds = invoices.Select(i => i.Id).ToList();
            decimal totalItemsSold = invoiceIds.Count > 0
                ? await db.TicketInvoiceLines
                    .Where(l => invoiceIds.Contains(l.TicketInvoiceId))
                    .SumAsync(l => l.Quantity, ct)
                : 0;

            // --- Cancelled items in period ---
            var cancelledItems = ticketIds.Count > 0
                ? await db.OrderItems
                    .Where(oi => ticketIds.Contains(oi.TicketId) && oi.Status == OrderItemStatus.Cancelled)
                    .ToListAsync(ct)
                : [];

            // --- Open tickets ---
            var openQuery = db.Tickets.Where(t => t.Status == TicketStatus.Open);
            if (q.Filter.CounterId.HasValue)
                openQuery = openQuery.Where(t => t.CounterId == q.Filter.CounterId.Value);
            int openTicketCount = await openQuery.CountAsync(ct);

            // --- Cancelled tickets in period ---
            var cancelledQuery = db.Tickets.Where(t => t.Status == TicketStatus.Cancelled
                && t.CancelledAt >= from && t.CancelledAt <= to);
            if (q.Filter.CounterId.HasValue)
                cancelledQuery = cancelledQuery.Where(t => t.CounterId == q.Filter.CounterId.Value);
            int cancelledTicketCount = await cancelledQuery.CountAsync(ct);

            // --- Compute metrics ---
            int billCount = invoices.Count;
            decimal totalRevenue = invoices.Sum(i => i.TotalAmount);
            decimal totalSubtotal = invoices.Sum(i => i.Subtotal);
            decimal totalDiscount = invoices.Sum(i => i.DiscountAmount);
            decimal totalSc = invoices.Sum(i => i.ServiceChargeAmount);
            decimal totalVat = invoices.Sum(i => i.VatAmount);
            decimal totalRounding = invoices.Sum(i => i.RoundingAdjustment);
            int totalGuests = invoices.Sum(i => (int)i.GuestCount);
            decimal avgBill = billCount > 0 ? totalRevenue / billCount : 0;
            decimal revPerGuest = totalGuests > 0 ? totalRevenue / totalGuests : 0;
            double totalHours = (to - from).TotalHours;
            decimal revPerHour = totalHours > 0 ? (decimal)((double)totalRevenue / totalHours) : 0;

            decimal avgItemsPerBill = billCount > 0 ? totalItemsSold / billCount : 0;
            decimal avgGuestsPerBill = billCount > 0 ? (decimal)totalGuests / billCount : 0;

            int discountedBillCount = invoices.Count(i => i.DiscountAmount > 0);
            decimal discountRate = billCount > 0 ? (decimal)discountedBillCount / billCount * 100 : 0;
            decimal avgDiscountPerDiscounted = discountedBillCount > 0
                ? totalDiscount / discountedBillCount : 0;
            decimal totalDiscountPct = totalSubtotal > 0 ? totalDiscount / totalSubtotal * 100 : 0;

            decimal cashAmount = payments.Where(p => p.PaymentMethod.Code == "CASH").Sum(p => p.Amount);
            decimal qrAmount = payments.Where(p => p.PaymentMethod.Code == "QR").Sum(p => p.Amount);
            decimal totalPaid = payments.Sum(p => p.Amount);
            decimal cashRate = totalPaid > 0 ? cashAmount / totalPaid * 100 : 0;
            decimal qrRate = totalPaid > 0 ? qrAmount / totalPaid * 100 : 0;
            decimal avgPaymentCountPerBill = billCount > 0 ? (decimal)payments.Count / billCount : 0;
            decimal paidVsDelta = totalPaid - totalRevenue;

            double avgServiceDuration = invoices.Count > 0
                ? invoices.Average(i => (i.ClosedAt - i.OpenedAt).TotalMinutes) : 0;
            int cancelledItemCount = cancelledItems.Count;
            decimal cancelRate = totalItemsSold + cancelledItemCount > 0
                ? (decimal)cancelledItemCount / (totalItemsSold + cancelledItemCount) * 100 : 0;
            decimal refundAmount = cancelledItems.Sum(oi => oi.LineTotal);

            // --- Comparison metrics ---
            var halfSpan = TimeSpan.FromTicks((to - from).Ticks / 2);
            var prevFrom = from - halfSpan;
            var prevTo = from;
            decimal prevPeriodRevenue = await db.TicketInvoices
                .Where(i => i.ClosedAt >= prevFrom && i.ClosedAt <= prevTo).SumAsync(i => i.TotalAmount, ct);
            decimal prevChangePct = prevPeriodRevenue > 0
                ? (totalRevenue - prevPeriodRevenue) / prevPeriodRevenue * 100 : 0;

            var sameDowFrom = from.AddDays(-7);
            var sameDowTo = to.AddDays(-7);
            decimal sameDowRevenue = await db.TicketInvoices
                .Where(i => i.ClosedAt >= sameDowFrom && i.ClosedAt <= sameDowTo).SumAsync(i => i.TotalAmount, ct);
            decimal sameDowChangePct = sameDowRevenue > 0
                ? (totalRevenue - sameDowRevenue) / sameDowRevenue * 100 : 0;

            var thirtyFrom = now.AddDays(-30);
            var thirtyTo = now;
            decimal thirtyDayRevenue = await db.TicketInvoices
                .Where(i => i.ClosedAt >= thirtyFrom && i.ClosedAt <= thirtyTo).SumAsync(i => i.TotalAmount, ct);
            int thirtyDayBills = await db.TicketInvoices
                .Where(i => i.ClosedAt >= thirtyFrom && i.ClosedAt <= thirtyTo).CountAsync(ct);
            decimal thirtyDayAvgRevenue = thirtyDayBills > 0 ? thirtyDayRevenue / thirtyDayBills * billCount : 0;
            decimal vsThirtyDayPct = thirtyDayAvgRevenue > 0
                ? (totalRevenue - thirtyDayAvgRevenue) / thirtyDayAvgRevenue * 100 : 0;

            // --- Breakdown ---
            var breakdown = BuildBreakdown(q.GroupBy ?? "day", invoices, payments);

            return Result.Success(new Response(
                totalRevenue, totalSubtotal, totalDiscount, totalSc, totalVat, totalRounding,
                avgBill, revPerGuest, revPerHour,
                billCount, totalGuests, totalItemsSold, avgItemsPerBill, avgGuestsPerBill,
                discountedBillCount, discountRate, avgDiscountPerDiscounted, totalDiscountPct,
                cashAmount, qrAmount, cashRate, qrRate, totalPaid, avgPaymentCountPerBill, paidVsDelta,
                avgServiceDuration, cancelledItemCount, cancelRate, refundAmount,
                openTicketCount, cancelledTicketCount,
                prevPeriodRevenue, prevChangePct, sameDowRevenue, sameDowChangePct,
                thirtyDayAvgRevenue, vsThirtyDayPct,
                breakdown));
        }

        private static IReadOnlyList<BreakdownRow> BuildBreakdown(
            string groupBy, List<TicketInvoice> invoices, List<TicketPaymentDetail> payments)
        {
            var payByTicket = payments.GroupBy(p => p.TicketId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return groupBy switch
            {
                "hour" => invoices.GroupBy(i => i.ClosedAt.Hour).OrderBy(g => g.Key)
                    .Select(g => MakeRow($"{g.Key:D2}:00", g.ToList(), payByTicket)).ToList(),
                "counter" => invoices.GroupBy(i => i.CounterId)
                    .Select(g => MakeRow($"Counter #{g.Key}", g.ToList(), payByTicket)).ToList(),
                "area" => invoices.GroupBy(i => i.AreaId)
                    .Select(g => MakeRow($"Area #{g.Key}", g.ToList(), payByTicket)).ToList(),
                "shift" => invoices.GroupBy(i => i.ShiftId)
                    .Select(g => MakeRow($"Shift #{g.Key}", g.ToList(), payByTicket)).ToList(),
                _ => invoices.GroupBy(i => i.ClosedAt.Date).OrderBy(g => g.Key)
                    .Select(g => MakeRow(g.Key.ToString("yyyy-MM-dd"), g.ToList(), payByTicket)).ToList()
            };
        }

        private static BreakdownRow MakeRow(string label, List<TicketInvoice> invs,
            Dictionary<long, List<TicketPaymentDetail>> payByTicket)
        {
            int count = invs.Count;
            decimal rev = invs.Sum(i => i.TotalAmount);
            decimal sub = invs.Sum(i => i.Subtotal);
            decimal disc = invs.Sum(i => i.DiscountAmount);
            decimal sc = invs.Sum(i => i.ServiceChargeAmount);
            decimal vat = invs.Sum(i => i.VatAmount);
            int guests = invs.Sum(i => (int)i.GuestCount);
            var tids = invs.Select(i => i.TicketId).ToList();
            var pays = tids.SelectMany(tid => payByTicket.GetValueOrDefault(tid, [])).ToList();
            decimal cash = pays.Where(p => p.PaymentMethod.Code == "CASH").Sum(p => p.Amount);
            decimal qr = pays.Where(p => p.PaymentMethod.Code == "QR").Sum(p => p.Amount);
            double avgDur = count > 0
                ? invs.Average(i => (i.ClosedAt - i.OpenedAt).TotalMinutes) : 0;
            return new BreakdownRow(label, rev, sub, disc, sc, vat,
                count > 0 ? rev / count : 0, count, guests, cash, qr,
                pays.Sum(p => p.Amount), (decimal)avgDur);
        }
    }
}
