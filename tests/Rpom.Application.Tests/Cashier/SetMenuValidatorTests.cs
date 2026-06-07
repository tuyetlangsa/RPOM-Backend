using FluentAssertions;
using Rpom.Application.Cashier.AddCartItem;
using Rpom.Domain.Sales;

namespace Rpom.Application.Tests.Cashier;

/// <summary>Pure-unit coverage of the set-menu selection validator (no DB).</summary>
public class SetMenuValidatorTests
{
    // Set menu: 1 fixed component (item 10), 1 optional (item 11);
    // 1 choice category (cc 100, choose 1..2) with modifiers item 20 (+5k, qty 1..2) and 21 (+0, qty 1..1).
    private static SetMenuValidator.Spec BuildSpec() => new(
        Components: new[]
        {
            new SetMenuValidator.ComponentSpec(10, IsFixed: true),
            new SetMenuValidator.ComponentSpec(11, IsFixed: false),
        },
        ChoiceCategories: new[]
        {
            new SetMenuValidator.ChoiceCategorySpec(100, MinChoice: 1, MaxChoice: 2, new[]
            {
                new SetMenuValidator.ModifierSpec(20, MinPerModifier: 1, MaxPerModifier: 2, ExtraPrice: 5000m),
                new SetMenuValidator.ModifierSpec(21, MinPerModifier: 1, MaxPerModifier: 1, ExtraPrice: 0m),
            }),
        });

    private static SetMenuValidator.Selection Comp(int itemId) =>
        new(null, itemId, ComponentType.MainComponent, 1m);
    private static SetMenuValidator.Selection Mod(int ccId, int itemId, decimal qty) =>
        new(ccId, itemId, ComponentType.Modifier, qty);

    [Fact]
    public void Valid_FixedComponent_OneModifier_ComputesChoicePrice()
    {
        var r = SetMenuValidator.Validate(BuildSpec(), new[] { Comp(10), Mod(100, 20, 2m) });
        r.IsValid.Should().BeTrue();
        r.ChoicePricePerUnit.Should().Be(10000m); // 5000 × 2
    }

    [Fact]
    public void Missing_FixedComponent_Invalid()
    {
        var r = SetMenuValidator.Validate(BuildSpec(), new[] { Mod(100, 20, 1m) });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Component_NotInSetMenu_Invalid()
    {
        var r = SetMenuValidator.Validate(BuildSpec(), new[] { Comp(10), Comp(999), Mod(100, 20, 1m) });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void TooFewChoices_BelowMin_Invalid()
    {
        var r = SetMenuValidator.Validate(BuildSpec(), new[] { Comp(10) }); // 0 < MinChoice 1
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void TooManyChoices_AboveMax_Invalid()
    {
        // three distinct modifiers but only 20 and 21 exist; add 21 twice won't be distinct.
        // Use a spec with max 1 to exercise the ceiling.
        var spec = new SetMenuValidator.Spec(
            new[] { new SetMenuValidator.ComponentSpec(10, true) },
            new[]
            {
                new SetMenuValidator.ChoiceCategorySpec(100, 1, 1, new[]
                {
                    new SetMenuValidator.ModifierSpec(20, 1, 1, 0m),
                    new SetMenuValidator.ModifierSpec(21, 1, 1, 0m),
                }),
            });
        var r = SetMenuValidator.Validate(spec, new[] { Comp(10), Mod(100, 20, 1m), Mod(100, 21, 1m) });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Modifier_QuantityAboveMax_Invalid()
    {
        var r = SetMenuValidator.Validate(BuildSpec(), new[] { Comp(10), Mod(100, 20, 3m) }); // max 2
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Modifier_NotOptionOfChoiceCategory_Invalid()
    {
        var r = SetMenuValidator.Validate(BuildSpec(), new[] { Comp(10), Mod(100, 77, 1m) });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Modifier_WrongOrMissingCc_Invalid()
    {
        var r = SetMenuValidator.Validate(BuildSpec(), new[]
        {
            Comp(10), new SetMenuValidator.Selection(999, 20, ComponentType.Modifier, 1m),
        });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void OptionalComponent_CanBeSkipped()
    {
        var r = SetMenuValidator.Validate(BuildSpec(), new[] { Comp(10), Mod(100, 21, 1m) });
        r.IsValid.Should().BeTrue();
        r.ChoicePricePerUnit.Should().Be(0m);
    }
}
