namespace Rpom.Application.Abstraction.Versioning;

/// <summary>
///     Closed catalog of scopes — flat enum, no per-id namespacing in v1.
///     See <c>docs/RPOM_Versioning_Strategy.md</c> for the full list of events
///     that bump each scope.
/// </summary>
public static class VersionScopes
{
    public const string Menu = "MENU";
    public const string Pricing = "PRICING";
    public const string FloorPlan = "FLOOR_PLAN";
    public const string Kitchen = "KITCHEN";
    public const string Access = "ACCESS";
    public const string Config = "CONFIG";

    /// <summary>All known scopes — used by the sync endpoint to default the response.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Menu, Pricing, FloorPlan, Kitchen, Access, Config
    };

    public static bool IsKnown(string scope) => All.Contains(scope);
}
