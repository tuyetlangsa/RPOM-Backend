using FluentAssertions;
using Rpom.Application.DiscountPolicies;
using Rpom.Domain.Operations;

namespace Rpom.Application.Tests.Operations;

public class DiscountEvaluatorTests
{
    private static DiscountEvaluator.ConditionSpec T(decimal threshold, string applyType = DiscountApplyType.Percent,
        decimal value = 10m, int? areaId = null)
        => new(threshold, null, null, areaId, applyType, value);

    private static DiscountEvaluator.ConditionSpec Q(int itemId, decimal qtyThreshold, string applyType = DiscountApplyType.Percent,
        decimal value = 10m, int? areaId = null)
        => new(null, itemId, qtyThreshold, areaId, applyType, value);

    private static DiscountEvaluator.ItemBucket B(int itemId, decimal qty, decimal subtotal = 0)
        => new(itemId, qty, subtotal);

    // ---- TICKET_THRESHOLD ----

    [Fact]
    public void TicketThreshold_SubtotalAbove_Matches()
    {
        var r = DiscountEvaluator.Evaluate(DiscountType.TicketThreshold, null, 1,
            600_000m, 1, [], new[] { T(500_000m) });
        r.Should().NotBeNull();
        r!.DiscountValue.Should().Be(10m);
    }

    [Fact]
    public void TicketThreshold_SubtotalBelow_NoMatch()
    {
        var r = DiscountEvaluator.Evaluate(DiscountType.TicketThreshold, null, 1,
            400_000m, 1, [], new[] { T(500_000m) });
        r.Should().BeNull();
    }

    [Fact]
    public void TicketThreshold_PicksHighestValue()
    {
        var r = DiscountEvaluator.Evaluate(DiscountType.TicketThreshold, null, 1,
            600_000m, 1, [], new[] { T(500_000m, value: 5m), T(600_000m, value: 15m) });
        r!.DiscountValue.Should().Be(15m);
    }

    [Fact]
    public void TicketThreshold_AreaScope_MatchOnlyWhenCorrect()
    {
        var conditions = new[] { T(500_000m, areaId: 2) };
        // AreaId 2 != 1 → no match
        DiscountEvaluator.Evaluate(DiscountType.TicketThreshold, null, 1,
            600_000m, 1, [], conditions).Should().BeNull();
        // AreaId 2 == 2 → match
        DiscountEvaluator.Evaluate(DiscountType.TicketThreshold, null, 1,
            600_000m, 2, [], conditions).Should().NotBeNull();
    }

    // ---- QUANTITY_ITEM ----

    [Fact]
    public void QuantityItem_EnoughQty_Matches()
    {
        var r = DiscountEvaluator.Evaluate(DiscountType.QuantityItem, null, 1,
            0, 1, new[] { B(10, 7m) }, new[] { Q(10, 5m) });
        r.Should().NotBeNull();
    }

    [Fact]
    public void QuantityItem_NotEnoughQty_NoMatch()
    {
        var r = DiscountEvaluator.Evaluate(DiscountType.QuantityItem, null, 1,
            0, 1, new[] { B(10, 3m) }, new[] { Q(10, 5m) });
        r.Should().BeNull();
    }

    [Fact]
    public void QuantityItem_PicksHighestValue()
    {
        var r = DiscountEvaluator.Evaluate(DiscountType.QuantityItem, null, 1,
            0, 1, new[] { B(10, 10m) }, new[] { Q(10, 5m, value: 5m), Q(10, 10m, value: 20m) });
        r!.DiscountValue.Should().Be(20m);
    }

    // ---- DaysOfWeek ----

    [Fact]
    public void DaysOfWeek_Matches_Proceeds()
    {
        var r = DiscountEvaluator.Evaluate(DiscountType.TicketThreshold, "1,3,5", 3, // Wed
            600_000m, 1, [], new[] { T(500_000m) });
        r.Should().NotBeNull();
    }

    [Fact]
    public void DaysOfWeek_NoMatch_ReturnsNull()
    {
        var r = DiscountEvaluator.Evaluate(DiscountType.TicketThreshold, "1,3,5", 2, // Tue
            600_000m, 1, [], new[] { T(500_000m) });
        r.Should().BeNull();
    }

    [Fact]
    public void DaysOfWeek_Null_AllDaysAllowed()
    {
        var r = DiscountEvaluator.Evaluate(DiscountType.TicketThreshold, null, 7,
            600_000m, 1, [], new[] { T(500_000m) });
        r.Should().NotBeNull();
    }

    // ---- FIXED ----

    [Fact]
    public void FixedAmount_ReturnsApplyTypeFixed()
    {
        var r = DiscountEvaluator.Evaluate(DiscountType.TicketThreshold, null, 1,
            600_000m, 1, [], new[] { T(500_000m, applyType: DiscountApplyType.Fixed, value: 50_000m) });
        r.Should().NotBeNull();
        r!.ApplyType.Should().Be(DiscountApplyType.Fixed);
        r.DiscountValue.Should().Be(50_000m);
    }
}
