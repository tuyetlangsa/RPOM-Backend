namespace Rpom.Domain.Operations;

/// <summary>StaffNotification.Type values (broadcast operational alerts to POS/Cashier).</summary>
public static class StaffNotificationType
{
    public const string ItemOutOfStock = "ITEM_OUT_OF_STOCK";
    public const string ItemBackInStock = "ITEM_BACK_IN_STOCK";
}
