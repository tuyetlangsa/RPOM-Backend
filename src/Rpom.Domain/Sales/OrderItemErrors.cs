using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

public static class OrderItemErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "OrderItem.NotFound",
        "Món trong lượt gửi bếp không tồn tại.");

    public static readonly Error NotPending = Error.Conflict(
        "OrderItem.NotPending",
        "Chỉ được thao tác với món đang ở trạng thái PENDING.");

    public static readonly Error NotProcessing = Error.Conflict(
        "OrderItem.NotProcessing",
        "Chỉ được thao tác với món đang ở trạng thái PROCESSING.");

    public static readonly Error NotReady = Error.Conflict(
        "OrderItem.NotReady",
        "Chỉ được thao tác với món đang ở trạng thái READY.");

    public static readonly Error WrongTicket = Error.BadRequest(
        "OrderItem.WrongTicket",
        "Một hoặc nhiều món không thuộc phiếu này.");

    public static readonly Error WrongStation = Error.Conflict(
        "OrderItem.WrongStation",
        "Một hoặc nhiều món không thuộc khu bếp của bạn.");

    public static readonly Error NotRefundable = Error.Conflict(
        "OrderItem.NotRefundable",
        "Chỉ có thể trả món đã/đang nấu (PROCESSING/READY/DONE). Món chưa nấu hãy dùng Huỷ.");

    public static readonly Error CannotRefundRefund = Error.Conflict(
        "OrderItem.CannotRefundRefund",
        "Không thể trả một dòng trả hàng.");

    public static readonly Error RefundQuantityExceeded = Error.Conflict(
        "OrderItem.RefundQuantityExceeded",
        "Số lượng trả vượt quá số lượng còn lại của món.");

    public static readonly Error NotReturnLine = Error.Conflict(
        "OrderItem.NotReturnLine",
        "Đây không phải dòng trả hàng (refund) — không thể xử lý trả hàng tại bếp.");

    public static readonly Error ReturnNotActionable = Error.Conflict(
        "OrderItem.ReturnNotActionable",
        "Dòng trả hàng đã được xử lý hoặc không ở trạng thái có thể xử lý.");
}
