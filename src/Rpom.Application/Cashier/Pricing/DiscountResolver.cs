using Rpom.Application.DiscountPolicies;
using Rpom.Domain.Operations;

namespace Rpom.Application.Cashier.Pricing;

/// <summary>
///     Pure (no DB) resolver for the single attached discount policy. Given the policy shape
///     and the current ticket/line state, returns the effective discount percents — deriving
///     FIXED amounts into a percent against the current subtotal, re-checking conditions, and
///     capping at 100%. Returns Applies=false when the policy no longer qualifies, so callers
///     remove the discount. Does NOT select among policies.
/// </summary>
public static class DiscountResolver
{
    public sealed record PolicySpec(
        string DiscountType,
        string? DaysOfWeek,
        IReadOnlyList<DiscountEvaluator.ConditionSpec> Conditions);

    public sealed record Result(
        bool Applies, decimal TicketDiscountPercent, decimal LineDiscountPercent, int? MatchedItemId)
    {
        public static readonly Result None = new(false, 0m, 0m, null);
    }

    public static Result Resolve(
        PolicySpec policy,
        int today,
        decimal ticketSubtotal,
        int ticketAreaId,
        IReadOnlyList<DiscountEvaluator.ItemBucket> buckets)
    {
        DiscountEvaluator.Result? eval = DiscountEvaluator.Evaluate(
            policy.DiscountType, policy.DaysOfWeek, today,
            ticketSubtotal, ticketAreaId, buckets, policy.Conditions);

        if (eval is null)
        {
            return Result.None;
        }

        decimal percent;
        if (eval.ApplyType == DiscountApplyType.Percent)
        {
            percent = eval.DiscountValue;
        }
        else if (policy.DiscountType == DiscountType.TicketThreshold)
        {
            percent = ticketSubtotal == 0m ? 0m : eval.DiscountValue / ticketSubtotal * 100m;
        }
        else
        {
            decimal itemSubtotal = buckets
                .Where(b => b.ItemId == eval.MatchedItemId)
                .Sum(b => b.TotalLineSubtotal);
            percent = itemSubtotal == 0m ? 0m : eval.DiscountValue / itemSubtotal * 100m;
        }

        if (percent > 100m)
        {
            percent = 100m;
        }

        return policy.DiscountType == DiscountType.TicketThreshold
            ? new Result(true, percent, 0m, null)
            : new Result(true, 0m, percent, eval.MatchedItemId);
    }
}
