using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

public static class TicketErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Ticket.NotFound",
        "Phiếu không tồn tại");

    public static readonly Error NotOpen = Error.Conflict(
        "Ticket.NotOpen",
        "Phiếu không ở trạng thái mở (OPEN).");

    public static readonly Error NoOpenCashDrawer = Error.Conflict(
        "Ticket.NoOpenCashDrawer",
        "Quầy chưa mở ca tiền mặt. Hãy mở ca trước khi mở bàn.");

    public static readonly Error ShiftNotFound = Error.NotFound(
        "Ticket.ShiftNotFound",
        "Ca làm việc không tồn tại.");

    public static readonly Error NotFullyPaid = Error.Conflict(
        "Ticket.NotFullyPaid",
        "Hoá đơn chưa thanh toán đủ — không thể đóng.");

    public static readonly Error HasProcessingOrder = Error.Conflict(
        "Ticket.HasProcessingOrder",
        "Hoá đơn còn item đợt gọi món đang chế biến (PROCESSING). Hãy hoàn tất (DONE) hoặc huỷ các món/đợt đó trước khi đóng.");
}
