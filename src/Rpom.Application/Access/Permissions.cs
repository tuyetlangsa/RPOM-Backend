namespace Rpom.Application.Access;

/// <summary>
///     Permission catalog — source of truth for permission codes.
///     <para>
///         Convention: <c>{aggregate}:{action}</c> colon-namespaced (e.g. <c>ticket:open</c>).
///         Strings used as policy names in <c>.RequireAuthorization("...")</c> AND
///         as <c>Permission.Code</c> column values seeded into DB by AccessSeeder.
///     </para>
///     <para>
///         To add a new permission: add a const here, then add a row in AccessSeeder
///         with the matching PermissionGroup. No DB migration needed.
///     </para>
/// </summary>
public static class Permissions
{
    // ============ Common ============
    public const string StaffLogin = "staff:login";

    // ============ Master Data (Owner/Manager via NextERP) ============
    public const string MasterDataView = "master_data:view";
    public const string MasterDataManage = "master_data:manage";

    // ============ POS — Order Staff + Cashier ============
    public const string TicketOpen = "ticket:open";
    public const string TicketViewDetail = "ticket:view_detail";
    public const string TicketTransfer = "ticket:transfer";
    public const string TicketMerge = "ticket:merge";
    public const string TicketSplit = "ticket:split";
    public const string TicketCancel = "ticket:cancel";
    public const string OrderAddItems = "order:add_items";
    public const string OrderSendKitchen = "order:send_kitchen";
    public const string OrderItemCancelPending = "order_item:cancel_pending";
    public const string OrderItemRefundLine = "order_item:refund_line";
    public const string ReservationCreate = "reservation:create";
    public const string ReservationCancel = "reservation:cancel";
    public const string NotificationView = "notification:view";

    // ============ KDS — Kitchen Staff ============
    public const string KdsView = "kds:view";
    public const string OrderItemStartCooking = "order_item:start_cooking";
    public const string OrderItemMarkReady = "order_item:mark_ready";
    public const string OrderItemMarkDone = "order_item:mark_done";
    public const string OrderItemProcessReturn = "order_item:process_return";
    public const string ItemToggleAvailability = "item:toggle_availability";

    // ============ Cashier — payment + cash drawer ============
    /// <summary>Open a cash drawer at a counter (count opening cash).</summary>
    public const string CashDrawerOpen = "cash_drawer:open";

    /// <summary>
    ///     Close a cash drawer (count closing cash). Can be issued to a different
    ///     staff than the opener — e.g. Manager force-closing on cashier behalf.
    /// </summary>
    public const string CashDrawerClose = "cash_drawer:close";

    public const string PaymentCash = "payment:cash";
    public const string PaymentQr = "payment:qr";
    public const string PaymentCancelPending = "payment:cancel_pending";
    public const string PaymentDeleteRecord = "payment:delete_record";
    public const string TicketApplyDiscount = "ticket:apply_discount";
    public const string TicketClose = "ticket:close";
    public const string EInvoiceIssue = "e_invoice:issue";

    // Cashier read APIs — load floor plan, tickets, and menu for NextCashier.
    public const string CashierFloorPlan = "floor_plan:view_cashier";
    public const string CashierViewTicket = "ticket:view_cashier";
    public const string CashierViewMenu = "menu:view_cashier";
    public const string CustomerDisplayPair = "customer_display:pair";

    // ============ Reporting — Manager/Owner ============
    public const string ReportRevenue = "report:revenue";
    public const string ReportShift = "report:shift";
    public const string ReportItemConsumption = "report:item_consumption";
    public const string ReportExportExcel = "report:export_excel";

    // ============ AI Operations Assistant ============
    public const string AiAsk = "ai:ask";
    public const string AiViewNotifications = "ai:view_notifications";

    // ============ Access Control — Owner ============
    public const string StaffAccountManage = "staff_account:manage";
    public const string RoleManage = "role:manage";
    public const string PermissionAssign = "permission:assign";

    // ============ Configuration — Owner/Manager ============
    public const string ConfigView = "config:view";
    public const string ConfigManage = "config:manage";

    /// <summary>Owner edits per-field rounding precision (RoundingConfig).</summary>
    public const string UpdateRoundingConfig = "rounding_config:update";
}
