using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

public static class PriceTableErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "PriceTable.NotFound",
        "Không tìm thấy bảng giá.");

    public static readonly Error CodeDuplicate = Error.Conflict(
        "PriceTable.CodeDuplicate",
        "Mã bảng giá đã tồn tại.");

    public static readonly Error DateRangeInvalid = Error.BadRequest(
        "PriceTable.DateRangeInvalid",
        "Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");

    public static readonly Error HasVariants = Error.Conflict(
        "PriceTable.HasVariants",
        "Không xoá được — bảng giá còn variant. Hãy xoá các variant trước.");

    public static readonly Error NoActivePriceTable = Error.Conflict(
        "Menu.NoActivePriceTable",
        "Chưa có bảng giá hoạt động — gọi Owner");
}
