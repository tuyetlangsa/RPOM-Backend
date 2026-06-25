namespace Rpom.Application.Access;

/// <summary>
///     Module code catalog — top tier of the navigation-access tree. Seeded by AccessSeeder.
///     Codes used as <c>Module.Code</c> column values. Mirrors <see cref="Roles" />.
/// </summary>
public static class Modules
{
    public const string NextErp = "nexterp";
    public const string Cashier = "cashier";
    public const string Order = "order";
    public const string Kitchen = "kitchen";
}
