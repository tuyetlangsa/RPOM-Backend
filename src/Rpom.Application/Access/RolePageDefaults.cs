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
                Pages.NextErpDashboard, Pages.NextErpItems, Pages.NextErpCategories,
                Pages.NextErpMenu, Pages.NextErpPriceTables, Pages.NextErpDiscountPolicies,
                Pages.NextErpAreasTables, Pages.NextErpCounters, Pages.NextErpShifts,
                Pages.NextErpKitchenStations, Pages.NextErpInventory, Pages.NextErpStaffAccounts,
                Pages.NextErpRolesPermissions, Pages.NextErpConfig, Pages.NextErpReports,
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
