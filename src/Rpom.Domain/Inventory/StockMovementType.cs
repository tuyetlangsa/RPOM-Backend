namespace Rpom.Domain.Inventory;

/// <summary>
///     StockMovement.MovementType values.
///     STOCK_IN (+): Owner nhập kho mới.
///     ADJUST_IN (+): Owner điều chỉnh tăng (kiểm kê thấy thêm, hoàn lại).
///     ADJUST_OUT (-): hao hụt / hư hỏng / đổ bể / kiểm kê hụt.
///     DEDUCT (-): system auto, OrderItem PENDING → PROCESSING triggers deduction.
/// </summary>
public static class StockMovementType
{
    public const string StockIn = "STOCK_IN";
    public const string AdjustIn = "ADJUST_IN";
    public const string AdjustOut = "ADJUST_OUT";
    public const string Deduct = "DEDUCT";
}
