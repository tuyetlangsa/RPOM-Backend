using Rpom.Domain.Common;

namespace Rpom.Domain.Restaurant;

public static class CounterErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Counter.NotFound",
        "Không tìm thấy Quầy.");

    public static readonly Error NameRequired = Error.BadRequest(
        "Counter.NameRequired",
        "Tên Quầy là bắt buộc.");

    public static readonly Error InUse = Error.Conflict(
        "Counter.InUse",
        "Không xoá được vì Quầy đang có Khu sử dụng. Hãy chuyển hoặc xoá các Khu trước.");
}
