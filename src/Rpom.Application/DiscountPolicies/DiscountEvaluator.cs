using Rpom.Domain.Operations;

namespace Rpom.Application.DiscountPolicies;

/// <summary>
/// Pure (no DB) discount-policy condition evaluator. Given the ticket state and a policy,
/// finds the best-matching condition. For FIXED-amount conditions, the handler converts
/// the amount into per-line distributions — this evaluator only returns the winning condition.
/// </summary>
public static class DiscountEvaluator
{
    /// <summary>One item bucket from non-cancelled OrderItems for QUANTITY_ITEM matching.</summary>
    public sealed record ItemBucket(int ItemId, decimal TotalQuantity, decimal TotalLineSubtotal);

    /// <summary>A condition row in the shape the evaluator needs.</summary>
    public sealed record ConditionSpec(
        decimal? ThresholdAmount, int? ItemId, decimal? QuantityThreshold,
        int? AreaId, string ApplyType, decimal DiscountValue);

    /// <summary>Result of a successful evaluation.</summary>
    public sealed record Result(string ApplyType, decimal DiscountValue, int? MatchedItemId);

    /// <summary>
    /// Evaluate a policy's conditions against the current ticket state.
    /// Returns the best-matching condition (highest DiscountValue), or null if nothing matches.
    /// </summary>
    /// <param name="discountType">TICKET_THRESHOLD or QUANTITY_ITEM.</param>
    /// <param name="daysOfWeek">CSV "1..7" from the policy; null = all days.</param>
    /// <param name="today">Today's day-of-week number (1=Monday .. 7=Sunday).</param>
    /// <param name="ticketSubtotal">Current Ticket.Subtotal.</param>
    /// <param name="ticketAreaId">Current Ticket.AreaId.</param>
    /// <param name="items">Non-cancelled OrderItem buckets grouped by ItemId.</param>
    /// <param name="conditions">Condition rows of the policy.</param>
    public static Result? Evaluate(
        string discountType,
        string? daysOfWeek,
        int today,
        decimal ticketSubtotal,
        int ticketAreaId,
        IReadOnlyList<ItemBucket> items,
        IReadOnlyList<ConditionSpec> conditions)
    {
        // Check day-of-week gate.
        if (daysOfWeek is { Length: > 0 })
        {
            var allowed = daysOfWeek.Split(',').Select(d => int.Parse(d.Trim())).ToHashSet();
            if (!allowed.Contains(today))
            {
                return null;
            }
        }

        ConditionSpec? best = null;

        foreach (var c in conditions)
        {
            // Optional area scope.
            if (c.AreaId is { } areaId && areaId != ticketAreaId)
            {
                continue;
            }

            bool matched = discountType switch
            {
                DiscountType.TicketThreshold =>
                    c.ThresholdAmount is { } ta && ticketSubtotal >= ta,

                DiscountType.QuantityItem =>
                    c.ItemId is { } itemId && c.QuantityThreshold is { } qt
                    && items.Where(i => i.ItemId == itemId).Sum(i => i.TotalQuantity) >= qt,

                _ => false
            };

            if (!matched)
            {
                continue;
            }

            if (best is null || c.DiscountValue > best.DiscountValue)
            {
                best = c;
            }
        }

        return best is null
            ? null
            : new Result(best.ApplyType, best.DiscountValue, best.ItemId);
    }
}
