using FluentAssertions;
using Rpom.Application.DiscountPolicies.CreateDiscountPolicy;
using Rpom.Domain.Operations;

namespace Rpom.Application.Tests.Operations;

/// <summary>
/// Pure-unit coverage of CreateDiscountPolicy.Validator (no DB) — the FluentValidation
/// rules that run in the MediatR pipeline (and are bypassed when a handler is called
/// directly in the integration tests).
/// </summary>
public class DiscountPolicyValidatorTests
{
    private static CreateDiscountPolicy.Command Base(
        string discountType,
        params CreateDiscountPolicy.ConditionInput[] conditions)
        => new("DC", "Name", null, discountType, false, null, true, conditions);

    private static CreateDiscountPolicy.ConditionInput TicketCond(
        decimal threshold = 100000m, string applyType = DiscountApplyType.Percent, decimal value = 10m)
        => new(threshold, null, null, null, applyType, value, 1);

    private static CreateDiscountPolicy.ConditionInput ItemCond(int itemId = 1, decimal qty = 3m)
        => new(null, itemId, qty, null, DiscountApplyType.Percent, 10m, 1);

    private static bool Valid(CreateDiscountPolicy.Command cmd)
        => new CreateDiscountPolicy.Validator().Validate(cmd).IsValid;

    [Fact]
    public void ValidTicketThreshold_Passes()
        => Valid(Base(DiscountType.TicketThreshold, TicketCond())).Should().BeTrue();

    [Fact]
    public void ValidQuantityItem_Passes()
        => Valid(Base(DiscountType.QuantityItem, ItemCond())).Should().BeTrue();

    [Fact]
    public void EmptyConditions_Fails()
        => Valid(Base(DiscountType.TicketThreshold)).Should().BeFalse();

    [Fact] // TICKET_THRESHOLD condition carrying itemId/quantity → discriminator mismatch
    public void Discriminator_TicketWithItemFields_Fails()
        => Valid(Base(DiscountType.TicketThreshold, ItemCond())).Should().BeFalse();

    [Fact] // QUANTITY_ITEM condition carrying thresholdAmount → discriminator mismatch
    public void Discriminator_QuantityWithThreshold_Fails()
        => Valid(Base(DiscountType.QuantityItem, TicketCond())).Should().BeFalse();

    [Fact]
    public void PercentOver100_Fails()
        => Valid(Base(DiscountType.TicketThreshold,
            TicketCond(applyType: DiscountApplyType.Percent, value: 150m))).Should().BeFalse();

    [Fact] // FIXED amount has no 0..100 ceiling
    public void FixedOver100_Passes()
        => Valid(Base(DiscountType.TicketThreshold,
            TicketCond(applyType: DiscountApplyType.Fixed, value: 50000m))).Should().BeTrue();

    [Fact]
    public void NegativeDiscountValue_Fails()
        => Valid(Base(DiscountType.TicketThreshold,
            TicketCond(applyType: DiscountApplyType.Fixed, value: -1m))).Should().BeFalse();

    [Fact]
    public void InvalidApplyType_Fails()
        => Valid(Base(DiscountType.TicketThreshold,
            new CreateDiscountPolicy.ConditionInput(100000m, null, null, null, "BOGUS", 10m, 1)))
            .Should().BeFalse();

    [Fact]
    public void InvalidDiscountType_Fails()
        => Valid(Base("BOGUS", TicketCond())).Should().BeFalse();

    [Fact] // two TICKET_THRESHOLD conditions with same (threshold, area) → duplicate
    public void DuplicateTicketTrigger_Fails()
        => Valid(Base(DiscountType.TicketThreshold,
            TicketCond(threshold: 500000m, value: 10m),
            TicketCond(threshold: 500000m, value: 20m))).Should().BeFalse();

    [Fact] // same threshold, different area scope → allowed
    public void SameThresholdDifferentArea_Passes()
        => Valid(Base(DiscountType.TicketThreshold,
            new CreateDiscountPolicy.ConditionInput(500000m, null, null, null, DiscountApplyType.Fixed, 50000m, 1),
            new CreateDiscountPolicy.ConditionInput(500000m, null, null, 7, DiscountApplyType.Fixed, 60000m, 2)))
            .Should().BeTrue();

    [Fact] // two QUANTITY_ITEM conditions identical on (item, quantity, area) → duplicate
    public void DuplicateQuantityTrigger_Fails()
        => Valid(Base(DiscountType.QuantityItem,
            new CreateDiscountPolicy.ConditionInput(null, 5, 3m, null, DiscountApplyType.Percent, 10m, 1),
            new CreateDiscountPolicy.ConditionInput(null, 5, 3m, null, DiscountApplyType.Percent, 15m, 2)))
            .Should().BeFalse();

    [Fact] // same item, different quantity thresholds (tiered) → allowed
    public void SameItemDifferentQuantity_Passes()
        => Valid(Base(DiscountType.QuantityItem,
            new CreateDiscountPolicy.ConditionInput(null, 5, 3m, null, DiscountApplyType.Percent, 10m, 1),
            new CreateDiscountPolicy.ConditionInput(null, 5, 5m, null, DiscountApplyType.Percent, 20m, 2)))
            .Should().BeTrue();
}
