namespace Rpom.Application.Access;

/// <summary>
/// System role codes — seeded by AccessSeeder. Role is a LABEL only on
/// StaffAccount (display / filter / report); does NOT carry permissions.
/// Per CLAUDE.md, permissions are assigned per-account via StaffAccountPermission.
/// </summary>
public static class Roles
{
    public const string Owner = "OWNER";
    public const string Manager = "MANAGER";
    public const string Cashier = "CASHIER";
    public const string OrderStaff = "ORDER_STAFF";
    public const string KitchenStaff = "KITCHEN_STAFF";
    public const string AdminVendor = "ADMIN_VENDOR";
}
