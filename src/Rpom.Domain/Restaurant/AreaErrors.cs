using Rpom.Domain.Common;

namespace Rpom.Domain.Restaurant;

public static class AreaErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Area.NotFound",
        "Không tìm thấy Khu.");

    public static readonly Error CounterNotFound = Error.NotFound(
        "Area.CounterNotFound",
        "Quầy được tham chiếu không tồn tại.");

    public static readonly Error NameRequired = Error.BadRequest(
        "Area.NameRequired",
        "Tên Khu là bắt buộc.");

    public static readonly Error InUse = Error.Conflict(
        "Area.InUse",
        "Không xoá được vì Khu đang có Bàn sử dụng. Hãy chuyển hoặc xoá các Bàn trước.");
}
