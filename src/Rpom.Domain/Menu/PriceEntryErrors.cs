using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

public static class PriceEntryErrors
{
    public static readonly Error VariantNotFound = Error.NotFound(
        "PriceEntry.VariantNotFound",
        "Không tìm thấy variant giá.");

    public static readonly Error ItemNotFound = Error.BadRequest(
        "PriceEntry.ItemNotFound",
        "Một hoặc nhiều Item trong danh sách giá không tồn tại.");

    public static readonly Error PriceNegative = Error.BadRequest(
        "PriceEntry.PriceNegative",
        "Giá không được âm.");

    public static readonly Error DuplicateItem = Error.BadRequest(
        "PriceEntry.DuplicateItem",
        "Một Item xuất hiện nhiều lần trong danh sách giá.");
}
