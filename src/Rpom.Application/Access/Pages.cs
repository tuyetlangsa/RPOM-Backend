namespace Rpom.Application.Access;

/// <summary>
///     Page code catalog — second tier of the navigation-access tree, grouped by module.
///     Codes used as <c>Page.Code</c> column values, seeded by AccessSeeder. Mirrors
///     <see cref="Permissions" />. FE maps each code to a route; BE stores only the code.
/// </summary>
public static class Pages
{
    // ============ NextERP (admin web) ============
    // 1 page = 1 desktop window in NextERP/data/subsystems.ts (win != null).
    // Implemented (17 — seeded):

    // Mặt bằng
    public const string NextErpCounters = "nexterp.counters";
    public const string NextErpAreas = "nexterp.areas";
    public const string NextErpAreaMenuCategory = "nexterp.area_menu_category";
    public const string NextErpTables = "nexterp.tables";

    // Thực đơn
    public const string NextErpItems = "nexterp.items";
    public const string NextErpUom = "nexterp.uom";
    public const string NextErpUomConversion = "nexterp.uom_conversion";
    public const string NextErpChoiceCategories = "nexterp.choice_categories";
    public const string NextErpSetMenu = "nexterp.set_menu";
    public const string NextErpKitchenStations = "nexterp.kitchen_stations";

    // Kho
    public const string NextErpStock = "nexterp.stock";
    public const string NextErpStockMovement = "nexterp.stock_movement";

    // Giá & Khuyến mãi
    public const string NextErpPricing = "nexterp.pricing";
    public const string NextErpDiscountPolicies = "nexterp.discount_policies";

    // Hệ thống
    public const string NextErpStaffAccounts = "nexterp.staff_accounts";
    public const string NextErpShifts = "nexterp.shifts";
    public const string NextErpCancellationReasons = "nexterp.cancellation_reasons";

    // Future (window chưa build — declared but NOT seeded until UI exists):
    public const string NextErpFloorPlan = "nexterp.floor_plan";
    public const string NextErpServiceCharge = "nexterp.service_charge";
    public const string NextErpSchedule = "nexterp.schedule";
    public const string NextErpConfig = "nexterp.config";
    public const string NextErpReports = "nexterp.reports";
    public const string NextErpAi = "nexterp.ai";

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
