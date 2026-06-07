using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

public static class OrderErrors
{
    public static readonly Error EmptyCart = Error.Conflict(
        "Order.EmptyCart",
        "Giỏ hàng trống, không có gì để gửi bếp.");

    public static readonly Error CartItemNotFound = Error.NotFound(
        "Order.CartItemNotFound",
        "Dòng giỏ hàng không tồn tại trên phiếu này.");

    public static readonly Error ItemNotFound = Error.NotFound(
        "Order.ItemNotFound",
        "Mặt hàng không tồn tại hoặc đã ngừng bán.");

    public static readonly Error ItemNotPriced = Error.Conflict(
        "Order.ItemNotPriced",
        "Mặt hàng chưa có giá trong bảng giá đang áp dụng.");

    public static readonly Error InvalidSetMenuSelection = Error.BadRequest(
        "Order.InvalidSetMenuSelection",
        "Lựa chọn set menu không hợp lệ (sai thành phần hoặc vi phạm số lượng tối thiểu/tối đa).");

    public static readonly Error DetailsNotAllowed = Error.BadRequest(
        "Order.DetailsNotAllowed",
        "Mặt hàng đơn không nhận thành phần/tuỳ chọn.");
}
