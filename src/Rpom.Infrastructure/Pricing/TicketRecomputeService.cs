using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Cashier.Pricing;
using Rpom.Application.DiscountPolicies;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Pricing;

internal sealed class TicketRecomputeService(
    IDbContext dbContext,
    IRoundingConfig rc,
    IDateTimeProvider clock)
    : ITicketRecomputeService
{
    public async Task RecomputeAsync(long ticketId, CancellationToken ct)
    {
        Ticket ticket = await dbContext.Tickets
            .FirstAsync(t => t.Id == ticketId, ct);

        List<OrderItem> orderItems = await dbContext.OrderItems
            .Where(o => o.TicketId == ticketId && o.Status != OrderItemStatus.Cancelled && o.Quantity != 0)
            .ToListAsync(ct);

        DateTime now = clock.UtcNow;

        // ---- Re-derive the attached discount policy (attached-policy only, no re-selection) ----
        decimal ticketDiscountPercent = 0m;
        var lineDiscountPercentByItem = new Dictionary<int, decimal>();
        if (ticket.DiscountPolicyId is { } policyId)
        {
            var policy = await dbContext.DiscountPolicies
                .Where(p => p.Id == policyId && p.IsActive)
                .Include(p => p.Conditions)
                .FirstOrDefaultAsync(ct);

            if (policy is null)
            {
                ticket.DiscountPolicyId = null;   // policy deleted/deactivated
            }
            else
            {
                // Provisional subtotal from current non-cancelled, non-zero lines.
                decimal subtotalNow = orderItems.Sum(o =>
                    Money.Round(o.Quantity * (o.UnitPrice + o.ChoicePricePerUnit), rc, RoundingKeys.LineSubtotal));

                var buckets = orderItems
                    .GroupBy(o => o.ItemId)
                    .Select(g => new DiscountEvaluator.ItemBucket(
                        g.Key, g.Sum(o => o.Quantity),
                        g.Sum(o => Money.Round(o.Quantity * (o.UnitPrice + o.ChoicePricePerUnit), rc, RoundingKeys.LineSubtotal))))
                    .ToList();

                int today = ((int)clock.UtcNow.DayOfWeek + 6) % 7 + 1; // Mon=1..Sun=7

                var spec = new DiscountResolver.PolicySpec(
                    policy.DiscountType, policy.DaysOfWeek,
                    policy.Conditions.Select(c => new DiscountEvaluator.ConditionSpec(
                        c.ThresholdAmount, c.ItemId, c.QuantityThreshold,
                        c.AreaId, c.ApplyType, c.DiscountValue)).ToList());

                DiscountResolver.Result res = DiscountResolver.Resolve(
                    spec, today, subtotalNow, ticket.AreaId, buckets);

                if (!res.Applies)
                {
                    ticket.DiscountPolicyId = null;   // no longer qualifies → remove
                }
                else if (res.MatchedItemId is { } matchedId)
                {
                    lineDiscountPercentByItem[matchedId] = res.LineDiscountPercent;
                }
                else
                {
                    ticketDiscountPercent = res.TicketDiscountPercent;
                }
            }
        }

        ticket.DiscountPercent = ticketDiscountPercent;

        var lineResults = new List<LinePricingResult>(orderItems.Count);

        foreach (OrderItem o in orderItems)
        {
            // Net-negative item cleanup: if this item's net qty (incl. refund rows) <= 0, no discount.
            decimal netQty = orderItems.Where(x => x.ItemId == o.ItemId).Sum(x => x.Quantity);

            o.ServiceChargePercent = ticket.ServiceChargePercent;
            o.ServiceChargeVatPercent = ticket.ServiceChargeVatPercent;
            o.TicketDiscountPercent = netQty <= 0m ? 0m : ticketDiscountPercent;
            o.LineDiscountPercent = netQty <= 0m
                ? 0m
                : lineDiscountPercentByItem.GetValueOrDefault(o.ItemId, 0m);

            var input = new LinePricingInput(
                o.Quantity, o.UnitPrice, o.ChoicePricePerUnit,
                o.VatPercent, o.ServiceChargePercent, o.ServiceChargeVatPercent,
                o.LineDiscountPercent, o.TicketDiscountPercent);

            LinePricingResult r = PricingCalculator.ComputeLine(input, rc);

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

        HeaderPricingResult h = PricingCalculator.ComputeHeader(lineResults, rc);
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
        List<TicketItemSum> existing = await dbContext.TicketItemSums
            .Where(s => s.TicketId == ticket.Id)
            .ToListAsync(ct);
        dbContext.TicketItemSums.RemoveRange(existing);

        var groups = orderItems
            .GroupBy(o => new
            {
                o.ItemId,
                o.UomId,
                o.UnitPrice,
                o.ChoicePricePerUnit,
                o.LineDiscountPercent,
                o.TicketDiscountPercent,
                o.VatPercent,
                o.ServiceChargePercent,
                o.ServiceChargeVatPercent
            })
            .OrderBy(g => g.Min(o => o.SentAt))
            .ToList();

        int displayOrder = 1;
        foreach (var g in groups)
        {
            OrderItem first = g.First();
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
                UpdatedAt = now
            });
        }
    }
}
