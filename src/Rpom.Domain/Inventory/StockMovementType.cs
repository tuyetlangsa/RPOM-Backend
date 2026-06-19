namespace Rpom.Domain.Inventory;

/// <summary>
///     StockMovement.MovementType values.
///     STOCK_IN (+): Owner nhập kho mới.
///     ADJUST_IN (+): Owner điều chỉnh tăng (kiểm kê thấy thêm, hoàn lại).
///     ADJUST_OUT (-): hao hụt / hư hỏng / đổ bể / kiểm kê hụt.
///     DEDUCT (-): system auto, OrderItem PENDING → PROCESSING triggers deduction.
///     RETURN_IN (+): system auto, hoàn kho khi bếp xử lý dòng trả hàng (process-return).
/// </summary>
public static class StockMovementType
{
    public const string StockIn = "STOCK_IN";
    public const string AdjustIn = "ADJUST_IN";
    public const string AdjustOut = "ADJUST_OUT";
    public const string Deduct = "DEDUCT";
    public const string ReturnIn = "RETURN_IN";
}
