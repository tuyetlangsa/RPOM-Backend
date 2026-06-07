namespace Rpom.Application.Access;

/// <summary>
///     UI grouping of permissions (used purely for the permission-picker UI).
///     Does NOT participate in runtime auth — see <see cref="Permissions" />.
///     Codes used as <c>PermissionGroup.Code</c> column values in seed data.
/// </summary>
public static class PermissionGroups
{
    public const string Common = "common";
    public const string MasterData = "master_data";
    public const string Pos = "pos";
    public const string Kds = "kds";
    public const string Cashier = "cashier";
    public const string Reporting = "reporting";
    public const string Ai = "ai";
    public const string Access = "access";
}
