using Rpom.Domain.Common;

namespace Rpom.Domain.Inventory;

public static class StockMovementErrors
{
    public static readonly Error ItemNotFound = Error.NotFound(
        "StockMovement.ItemNotFound", "Khong tim thay item.");

    public static readonly Error ItemNotStockable = Error.BadRequest(
        "StockMovement.ItemNotStockable", "Chi item co IsStockable moi co the nhap/xuat kho.");

    public static readonly Error QuantityNotPositive = Error.BadRequest(
        "StockMovement.QuantityNotPositive", "So luong phai lon hon 0.");
}
