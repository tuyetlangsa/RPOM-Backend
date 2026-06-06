using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

public static class PriceVariantErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "PriceVariant.NotFound",
        "Không tìm thấy variant giá.");

    public static readonly Error PriceTableNotFound = Error.NotFound(
        "PriceVariant.PriceTableNotFound",
        "Không tìm thấy bảng giá cha.");

    public static readonly Error CodeDuplicate = Error.Conflict(
        "PriceVariant.CodeDuplicate",
        "Mã variant đã tồn tại trong bảng giá này.");

    public static readonly Error TimeRangeInvalid = Error.BadRequest(
        "PriceVariant.TimeRangeInvalid",
        "Khung giờ không hợp lệ: phải nhập cả giờ bắt đầu và kết thúc, BeginTime phải nhỏ hơn EndTime.");

    public static readonly Error DayMaskInvalid = Error.BadRequest(
        "PriceVariant.DayMaskInvalid",
        "DayMask không hợp lệ — phải trong khoảng 1..127.");

    public static readonly Error AreaListRequired = Error.BadRequest(
        "PriceVariant.AreaListRequired",
        "Khi AppliesToAllAreas=false, phải chọn ít nhất 1 khu.");

    public static readonly Error AreaListMustBeEmpty = Error.BadRequest(
        "PriceVariant.AreaListMustBeEmpty",
        "Khi AppliesToAllAreas=true, không được chọn khu cụ thể.");

    public static readonly Error AreaNotFound = Error.BadRequest(
        "PriceVariant.AreaNotFound",
        "Một hoặc nhiều khu được chọn không tồn tại.");

    public static readonly Error DefaultVariantExists = Error.Conflict(
        "PriceVariant.DefaultVariantExists",
        "Bảng giá đã có 1 variant catch-all (specificity=0). Không thể tạo variant catch-all thứ 2.");

    public static Error OverlapConflict(string conflictingCode) => Error.Conflict(
        "PriceVariant.OverlapConflict",
        $"Variant này xung đột với variant '{conflictingCode}' (cùng specificity và overlap cả 3 chiều Time/Day/Area). Hãy thay đổi điều kiện hoặc tăng specificity.");

    public static readonly Error HasEntries = Error.Conflict(
        "PriceVariant.HasEntries",
        "Không xoá được — variant còn entry giá. Hãy xoá các entry trước.");
}
