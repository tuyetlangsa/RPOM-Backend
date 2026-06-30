namespace Rpom.Application.Access;

/// <summary>
///     Default permission-code template per role. NOT a runtime gate — a convenience
///     applied via the "Apply role default" button on the permission tab. Owner is
///     omitted (bootstrap Owner gets everything); unknown roles get an empty default.
/// </summary>
public static class RolePermissionDefaults
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ByRole =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Roles.Manager] = new[]
            {
                Permissions.MasterDataView, Permissions.MasterDataManage,
                Permissions.TicketOpen, Permissions.TicketViewDetail, Permissions.TicketTransfer,
                Permissions.TicketMerge, Permissions.TicketSplit, Permissions.TicketCancel,
                Permissions.TicketList, Permissions.TicketAuditLog,
                Permissions.TicketApplyDiscount, Permissions.TicketClose,
                Permissions.OrderAddItems, Permissions.OrderSendKitchen,
                Permissions.CashDrawerOpen, Permissions.CashDrawerClose,
                Permissions.PaymentCash, Permissions.PaymentQr, Permissions.PaymentCancelPending,
                Permissions.ReservationView, Permissions.ReservationCreate,
                Permissions.ReservationSeat, Permissions.ReservationCancel,
                Permissions.CashierFloorPlan, Permissions.CashierViewTicket, Permissions.CashierViewMenu,
                Permissions.ReportRevenue, Permissions.ReportShift, Permissions.ReportItemConsumption,
                Permissions.ReportExportExcel, Permissions.ConfigView, Permissions.NotificationView
            },
            [Roles.Cashier] = new[]
            {
                Permissions.CashDrawerOpen, Permissions.CashDrawerClose,
                Permissions.PaymentCash, Permissions.PaymentQr, Permissions.PaymentCancelPending,
                Permissions.TicketOpen, Permissions.TicketViewDetail, Permissions.CashierViewTicket,
                Permissions.TicketApplyDiscount, Permissions.TicketClose, Permissions.EInvoiceIssue,
                Permissions.OrderAddItems, Permissions.OrderSendKitchen,
                Permissions.ReservationView, Permissions.ReservationCreate,
                Permissions.ReservationSeat, Permissions.ReservationCancel,
                Permissions.CashierFloorPlan, Permissions.CashierViewMenu, Permissions.NotificationView
            },
            [Roles.OrderStaff] = new[]
            {
                Permissions.TicketOpen, Permissions.TicketViewDetail,
                Permissions.OrderAddItems, Permissions.OrderSendKitchen,
                Permissions.OrderItemCancelPending, Permissions.OrderItemMarkDone,
                Permissions.ReservationView, Permissions.ReservationCreate,
                Permissions.ReservationSeat, Permissions.ReservationCancel,
                Permissions.NotificationView
            },
            [Roles.KitchenStaff] = new[]
            {
                Permissions.KdsView, Permissions.OrderItemStartCooking, Permissions.OrderItemMarkReady,
                Permissions.OrderItemProcessReturn, Permissions.ItemToggleAvailability
            }
        };

    /// <summary>Default permission codes for a role, or empty if the role has no template.</summary>
    public static IReadOnlyList<string> ForRole(string roleCode) =>
        ByRole.TryGetValue(roleCode, out IReadOnlyList<string>? perms)
            ? perms
            : Array.Empty<string>();
}
