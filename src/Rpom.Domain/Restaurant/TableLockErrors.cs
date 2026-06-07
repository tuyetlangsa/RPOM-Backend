using Rpom.Domain.Common;

namespace Rpom.Domain.Restaurant;

public static class TableLockErrors
{
    /// <summary>The caller does not hold a live lock on the table (required to write).</summary>
    public static readonly Error NotHeld = Error.Conflict(
        "TableLock.NotHeld",
        "Bạn chưa giữ quyền thao tác bàn này (hoặc đã hết hạn). Hãy mở lại bàn.");

    /// <summary>Another staff currently holds a live lock on the table.</summary>
    public static Error HeldByOther(string staffName) => Error.Conflict(
        "TableLock.HeldByOther",
        $"Bàn đang được {staffName} thao tác. Vui lòng đợi hoặc chọn bàn khác.");
}
