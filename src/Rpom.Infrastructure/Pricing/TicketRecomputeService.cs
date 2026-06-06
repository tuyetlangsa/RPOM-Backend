using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Pricing;

internal sealed class TicketRecomputeService(
    IDbContext dbContext, IRoundingConfig rc, IDateTimeProvider clock)
    : ITicketRecomputeService
{
    public async Task RecomputeAsync(long ticketId, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets
            .FirstAsync(t => t.Id == ticketId, ct);

        var orderItems = await dbContext.OrderItems
            .Where(o => o.TicketId == ticketId && o.Status != OrderItemStatus.Cancelled && o.Quantity != 0)
            .ToListAsync(ct);

        var now = clock.UtcNow;
        var lineResults = new List<LinePricingResult>(orderItems.Count);

        foreach (var o in orderItems)
        {
            // Step 0 — re-snapshot from ticket header (cashier may have just changed these)
            o.TicketDiscountPercent = ticket.DiscountPercent;
            o.ServiceChargePercent = ticket.ServiceChargePercent;
            o.ServiceChargeVatPercent = ticket.ServiceChargeVatPercent;

            var input = new LinePricingInput(
                o.Quantity, o.UnitPrice, o.ChoicePricePerUnit,
                o.VatPercent, o.ServiceChargePercent, o.ServiceChargeVatPercent,
                o.LineDiscountPercent, o.TicketDiscountPercent);

            var r = PricingCalculator.ComputeLine(input, rc);

            o.LineSubtotal = r.LineSubtotal;
            o.LineDiscountAmount = r.LineDiscountAmount;
            o.TicketDiscountAmount = r.TicketDiscountAmount;
            o.TotalDiscountAmount = r.TotalDiscountAmount;
            o.ServiceChargeAmount = r.ServiceChargeAmount;
            o.VatItemAmount = r.VatItemAmount;
            o.VatScAmount = r.VatScAmount;
            o.VatAmount = r.VatAmount;
            o.LineTotal = r.LineTotal;
            o.UpdatedAt = now;

            lineResults.Add(r);
        }

        await RebuildBucketsAsync(ticket, orderItems, now, ct);

        var h = PricingCalculator.ComputeHeader(lineResults, rc);
        ticket.Subtotal = h.Subtotal;
        ticket.LineDiscountTotal = h.LineDiscountTotal;
        ticket.TicketDiscountTotal = h.TicketDiscountTotal;
        ticket.DiscountAmount = h.DiscountAmount;
        ticket.ServiceChargeAmount = h.ServiceChargeAmount;
        ticket.VatAmount = h.VatAmount;
        ticket.TotalAmount = h.TotalAmount;
        ticket.RoundingAdjustment = h.RoundingAdjustment;
        ticket.UpdatedAt = now;
    }

    /// <summary>DELETE + INSERT bucket rows grouped by the 10-field bucket dimension.</summary>
    private async Task RebuildBucketsAsync(
        Ticket ticket, IReadOnlyList<OrderItem> orderItems, DateTime now, CancellationToken ct)
    {
        var existing = await dbContext.TicketItemSums
            .Where(s => s.TicketId == ticket.Id)
            .ToListAsync(ct);
        dbContext.TicketItemSums.RemoveRange(existing);

        var groups = orderItems
            .GroupBy(o => new
            {
                o.ItemId, o.UomId, o.UnitPrice, o.ChoicePricePerUnit,
                o.LineDiscountPercent, o.TicketDiscountPercent,
                o.VatPercent, o.ServiceChargePercent, o.ServiceChargeVatPercent
            })
            .OrderBy(g => g.Min(o => o.SentAt))
            .ToList();

        var displayOrder = 1;
        foreach (var g in groups)
        {
            var first = g.First();
            dbContext.TicketItemSums.Add(new TicketItemSum
            {
                TicketId = ticket.Id,
                ItemId = g.Key.ItemId,
                ItemCode = first.ItemCode,
                ItemName = first.ItemName,
                UomId = g.Key.UomId,
                UomCode = first.UomCode,
                UomName = first.UomName,
                UnitPrice = g.Key.UnitPrice,
                ChoicePricePerUnit = g.Key.ChoicePricePerUnit,
                LineDiscountPercent = g.Key.LineDiscountPercent,
                TicketDiscountPercent = g.Key.TicketDiscountPercent,
                VatPercent = g.Key.VatPercent,
                ServiceChargePercent = g.Key.ServiceChargePercent,
                ServiceChargeVatPercent = g.Key.ServiceChargeVatPercent,
                TotalQuantity = g.Sum(o => o.Quantity),
                TotalLineSubtotal = g.Sum(o => o.LineSubtotal),
                TotalDiscount = g.Sum(o => o.TotalDiscountAmount),
                TotalServiceCharge = g.Sum(o => o.ServiceChargeAmount),
                TotalVat = g.Sum(o => o.VatAmount),
                TotalAmount = g.Sum(o => o.LineTotal),
                MaxOrderItemId = g.Max(o => o.Id),
                DisplayOrder = displayOrder++,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
    }
}
