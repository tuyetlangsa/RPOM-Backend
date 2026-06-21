namespace Rpom.Application.Access;

/// <summary>
///     Default page-access template per role. NOT a runtime gate — a convenience
///     default applied when resetting an account's page access (and, later, when
///     creating a new account). Owner is intentionally omitted: the bootstrap Owner
///     is seeded with ALL pages by AccessSeeder, and custom roles get an empty default.
/// </summary>
public static class RolePageDefaults
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ByRole =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Roles.Manager] = new[]
            {
                // NextERP — all 17 implemented pages
                Pages.NextErpCounters, Pages.NextErpAreas, Pages.NextErpAreaMenuCategory,
                Pages.NextErpTables, Pages.NextErpItems, Pages.NextErpUom,
                Pages.NextErpUomConversion, Pages.NextErpChoiceCategories, Pages.NextErpSetMenu,
                Pages.NextErpKitchenStations, Pages.NextErpStock, Pages.NextErpStockMovement,
                Pages.NextErpPricing, Pages.NextErpDiscountPolicies, Pages.NextErpStaffAccounts,
                Pages.NextErpShifts, Pages.NextErpCancellationReasons,
                // Operations — Cashier
                Pages.CashierFloorPlan, Pages.CashierTickets, Pages.CashierPayment,
                Pages.CashierCashDrawer
            },
            [Roles.Cashier] = new[]
            {
                Pages.CashierFloorPlan, Pages.CashierTickets, Pages.CashierPayment,
                Pages.CashierCashDrawer
            },
            [Roles.OrderStaff] = new[]
            {
                Pages.OrderFloorPlan, Pages.OrderTickets, Pages.OrderMenu
            },
            [Roles.KitchenStaff] = new[]
            {
                Pages.KitchenKds, Pages.KitchenStations, Pages.KitchenIngredients
            }
        };

    /// <summary>Default page codes for a role, or empty if the role has no template.</summary>
    public static IReadOnlyList<string> ForRole(string roleCode) =>
        ByRole.TryGetValue(roleCode, out IReadOnlyList<string>? pages)
            ? pages
            : Array.Empty<string>();
}
