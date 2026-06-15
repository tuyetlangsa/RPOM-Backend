using FluentAssertions;
using Rpom.Application.Cashier.Pricing;
using Rpom.Application.DiscountPolicies;
using Rpom.Domain.Operations;
using Xunit;

namespace Rpom.Application.Tests.Pricing;

public sealed class DiscountResolverTests
{
    private static DiscountResolver.PolicySpec TicketThresholdFixed(decimal threshold, decimal amount) =>
        new(DiscountType.TicketThreshold, DaysOfWeek: null,
            new[] { new DiscountEvaluator.ConditionSpec(threshold, null, null, null, DiscountApplyType.Fixed, amount) });

    private static DiscountResolver.PolicySpec TicketThresholdPercent(decimal threshold, decimal percent) =>
        new(DiscountType.TicketThreshold, DaysOfWeek: null,
            new[] { new DiscountEvaluator.ConditionSpec(threshold, null, null, null, DiscountApplyType.Percent, percent) });

    [Fact]
    public void FixedTicketDiscount_DerivesPercentFromCurrentSubtotal()
    {
        var r = DiscountResolver.Resolve(
            TicketThresholdFixed(200_000m, 100_000m),
            today: 1, ticketSubtotal: 250_000m, ticketAreaId: 1, buckets: []);

        r.Applies.Should().BeTrue();
        r.TicketDiscountPercent.Should().BeApproximately(40m, 0.0001m);
        r.MatchedItemId.Should().BeNull();
    }

    [Fact]
    public void FixedTicketDiscount_TracksSubtotal_PercentShrinksAsBillGrows()
    {
        var r = DiscountResolver.Resolve(
            TicketThresholdFixed(200_000m, 100_000m),
            today: 1, ticketSubtotal: 500_000m, ticketAreaId: 1, buckets: []);

        r.TicketDiscountPercent.Should().BeApproximately(20m, 0.0001m);
    }

    [Fact]
    public void BelowThreshold_DoesNotApply()
    {
        var r = DiscountResolver.Resolve(
            TicketThresholdFixed(200_000m, 100_000m),
            today: 1, ticketSubtotal: 150_000m, ticketAreaId: 1, buckets: []);

        r.Applies.Should().BeFalse();
    }

    [Fact]
    public void PercentTicketDiscount_UsedDirectly()
    {
        var r = DiscountResolver.Resolve(
            TicketThresholdPercent(200_000m, 10m),
            today: 1, ticketSubtotal: 300_000m, ticketAreaId: 1, buckets: []);

        r.TicketDiscountPercent.Should().Be(10m);
    }

    [Fact]
    public void PercentCappedAt100()
    {
        var r = DiscountResolver.Resolve(
            TicketThresholdFixed(100_000m, 300_000m),
            today: 1, ticketSubtotal: 200_000m, ticketAreaId: 1, buckets: []);

        r.TicketDiscountPercent.Should().Be(100m);
    }
}
