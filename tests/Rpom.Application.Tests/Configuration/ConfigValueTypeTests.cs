using FluentAssertions;
using Rpom.Application.Configuration;

namespace Rpom.Application.Tests.Configuration;

public class ConfigValueTypeTests
{
    [Theory]
    [InlineData("12")]
    [InlineData("12.5")]
    [InlineData("-3")]
    [InlineData("0")]
    public void Number_AcceptsNumeric(string value)
        => ConfigValueType.IsValidValue(ConfigValueType.Number, value).Should().BeTrue();

    [Theory]
    [InlineData("abc")]
    [InlineData("12abc")]
    [InlineData("1,5")] // comma is not the invariant decimal separator
    public void Number_RejectsNonNumeric(string value)
        => ConfigValueType.IsValidValue(ConfigValueType.Number, value).Should().BeFalse();

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("True")]
    public void Bool_AcceptsBoolean(string value)
        => ConfigValueType.IsValidValue(ConfigValueType.Bool, value).Should().BeTrue();

    [Theory]
    [InlineData("yes")]
    [InlineData("1")]
    public void Bool_RejectsNonBoolean(string value)
        => ConfigValueType.IsValidValue(ConfigValueType.Bool, value).Should().BeFalse();

    [Theory]
    [InlineData("08:30")]
    [InlineData("23:59")]
    public void Time_AcceptsTime(string value)
        => ConfigValueType.IsValidValue(ConfigValueType.Time, value).Should().BeTrue();

    [Theory]
    [InlineData("25:00")]
    [InlineData("notatime")]
    public void Time_RejectsInvalidTime(string value)
        => ConfigValueType.IsValidValue(ConfigValueType.Time, value).Should().BeFalse();

    [Fact]
    public void Text_AcceptsAnything()
        => ConfigValueType.IsValidValue(ConfigValueType.Text, "whatever &^%").Should().BeTrue();

    [Fact]
    public void NullOrEmpty_AlwaysValid_MeansUnset()
    {
        ConfigValueType.IsValidValue(ConfigValueType.Number, null).Should().BeTrue();
        ConfigValueType.IsValidValue(ConfigValueType.Number, "").Should().BeTrue();
        ConfigValueType.IsValidValue(ConfigValueType.Number, "   ").Should().BeTrue();
    }

    [Fact]
    public void All_ContainsExactlyFourTypes()
        => ConfigValueType.All.Should().BeEquivalentTo(new[] { "BOOL", "TEXT", "NUMBER", "TIME" });
}
