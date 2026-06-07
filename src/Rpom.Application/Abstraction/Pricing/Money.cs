namespace Rpom.Application.Abstraction.Pricing;

/// <summary>
/// Single rounding entry point. NEAREST half-away-from-zero, hardcoded
/// (pricing spec §1). Every monetary round in the pricing pipeline goes
/// through here — never call Math.Round directly.
/// </summary>
public static class Money
{
    public static decimal Round(decimal value, IRoundingConfig config, string keyCode)
    {
        var digits = config.GetDigits(keyCode);
        return Math.Round(value, digits, MidpointRounding.AwayFromZero);
    }
}
