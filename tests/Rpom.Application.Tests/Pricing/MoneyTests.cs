using FluentAssertions;
using NSubstitute;
using Rpom.Application.Abstraction.Pricing;

namespace Rpom.Application.Tests.Pricing;

public class MoneyTests
{
    private static IRoundingConfig Rc(string key, int digits)
    {
        var rc = Substitute.For<IRoundingConfig>();
        rc.GetDigits(key).Returns(digits);
        return rc;
    }

    [Fact]
    public void Round_To2Digits_KeepsTwoDecimals()
    {
        var rc = Rc(RoundingKeys.LineSubtotal, 2);
        Money.Round(123.456m, rc, RoundingKeys.LineSubtotal).Should().Be(123.46m);
    }

    [Fact]
    public void Round_To0Digits_RoundsWholeVnd_AwayFromZero()
    {
        var rc = Rc(RoundingKeys.LineTotal, 0);
        Money.Round(120.5m, rc, RoundingKeys.LineTotal).Should().Be(121m);
    }

    [Fact]
    public void Round_NegativeHalf_RoundsAwayFromZero()
    {
        var rc = Rc(RoundingKeys.LineTotal, 0);
        Money.Round(-120.5m, rc, RoundingKeys.LineTotal).Should().Be(-121m);
    }
}
