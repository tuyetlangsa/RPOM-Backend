using Rpom.Domain.Common;

namespace Rpom.Domain.Operations;

public static class DiscountPolicyErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "DiscountPolicy.NotFound",
        "Không tìm thấy Chính sách giảm giá.");

    public static readonly Error CodeDuplicate = Error.Conflict(
        "DiscountPolicy.CodeDuplicate",
        "Mã Chính sách giảm giá đã tồn tại.");

    public static readonly Error InUse = Error.Conflict(
        "DiscountPolicy.InUse",
        "Không xoá được vì Chính sách giảm giá đang được Hoá đơn sử dụng. Hãy ngừng kích hoạt thay vì xoá.");

    public static readonly Error ItemNotFound = Error.NotFound(
        "DiscountPolicy.ItemNotFound",
        "Một hoặc nhiều Mặt hàng trong điều kiện không tồn tại.");

    public static readonly Error AreaNotFound = Error.NotFound(
        "DiscountPolicy.AreaNotFound",
        "Một hoặc nhiều Khu vực trong điều kiện không tồn tại.");
}
