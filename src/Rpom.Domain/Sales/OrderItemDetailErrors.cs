using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

public static class OrderItemDetailErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "OrderItemDetail.NotFound",
        "Không tìm thấy thành phần set menu.");

    public static readonly Error NotKitchenComponent = Error.Conflict(
        "OrderItemDetail.NotKitchenComponent",
        "Thành phần này không gắn khu bếp — không có vòng đời chế biến.");

    public static readonly Error WrongStation = Error.Conflict(
        "OrderItemDetail.WrongStation",
        "Một hoặc nhiều thành phần không thuộc khu bếp của bạn.");

    public static readonly Error NotPending = Error.Conflict(
        "OrderItemDetail.NotPending",
        "Chỉ bắt đầu chế biến thành phần đang ở trạng thái PENDING.");

    public static readonly Error NotProcessing = Error.Conflict(
        "OrderItemDetail.NotProcessing",
        "Chỉ đánh sẵn sàng thành phần đang ở trạng thái PROCESSING.");

    public static readonly Error NotReady = Error.Conflict(
        "OrderItemDetail.NotReady",
        "Chỉ đánh hoàn tất (DONE) thành phần đang ở trạng thái READY.");

    public static readonly Error WrongTicket = Error.BadRequest(
        "OrderItemDetail.WrongTicket",
        "Một hoặc nhiều thành phần không thuộc phiếu này.");
}
