namespace Rpom.Application.Access;

/// <summary>
///     Page code catalog — second tier of the navigation-access tree, grouped by module.
///     Codes used as <c>Page.Code</c> column values, seeded by AccessSeeder. Mirrors
///     <see cref="Permissions" />. FE maps each code to a route; BE stores only the code.
/// </summary>
public static class Pages
{
    // ============ NextERP ============
    public const string NextErpDashboard = "nexterp.dashboard";
    public const string NextErpItems = "nexterp.items";
    public const string NextErpCategories = "nexterp.categories";
    public const string NextErpMenu = "nexterp.menu";
    public const string NextErpPriceTables = "nexterp.price_tables";
    public const string NextErpDiscountPolicies = "nexterp.discount_policies";
    public const string NextErpAreasTables = "nexterp.areas_tables";
    public const string NextErpCounters = "nexterp.counters";
    public const string NextErpShifts = "nexterp.shifts";
    public const string NextErpKitchenStations = "nexterp.kitchen_stations";
    public const string NextErpInventory = "nexterp.inventory";
    public const string NextErpStaffAccounts = "nexterp.staff_accounts";
    public const string NextErpRolesPermissions = "nexterp.roles_permissions";
    public const string NextErpConfig = "nexterp.config";
    public const string NextErpReports = "nexterp.reports";

    // ============ Cashier ============
    public const string CashierFloorPlan = "cashier.floor_plan";
    public const string CashierTickets = "cashier.tickets";
    public const string CashierPayment = "cashier.payment";
    public const string CashierCashDrawer = "cashier.cash_drawer";

    // ============ Order ============
    public const string OrderFloorPlan = "order.floor_plan";
    public const string OrderTickets = "order.tickets";
    public const string OrderMenu = "order.menu";

    // ============ Kitchen ============
    public const string KitchenKds = "kitchen.kds";
    public const string KitchenStations = "kitchen.stations";
    public const string KitchenIngredients = "kitchen.ingredients";
}
