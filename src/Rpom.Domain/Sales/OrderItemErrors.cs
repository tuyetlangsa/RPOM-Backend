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
}
