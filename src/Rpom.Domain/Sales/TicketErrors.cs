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

    public static readonly Error TransferSameTable = Error.Conflict(
        "Ticket.TransferSameTable",
        "Bàn đích trùng với bàn hiện tại của phiếu.");

    public static readonly Error TransferCrossCounter = Error.Conflict(
        "Ticket.TransferCrossCounter",
        "Không thể chuyển phiếu sang bàn thuộc quầy khác.");

    public static readonly Error HasActiveItems = Error.Conflict(
        "Ticket.HasActiveItems",
        "Phiếu còn món chưa huỷ. Hãy huỷ hết món trước khi huỷ phiếu.");

    public static readonly Error HasPendingPayment = Error.Conflict(
        "Ticket.HasPendingPayment",
        "Phiếu còn thanh toán đang chờ. Hãy xử lý thanh toán trước khi huỷ phiếu.");

    public static readonly Error HasSuccessfulPayment = Error.Conflict(
        "Ticket.HasSuccessfulPayment",
        "Phiếu đã có thanh toán thành công nên không thể huỷ.");

    public static readonly Error InvalidCancellationReason = Error.BadRequest(
        "Ticket.InvalidCancellationReason",
        "Lý do huỷ không hợp lệ hoặc đã ngừng sử dụng.");

    public static readonly Error InvalidManager = Error.BadRequest(
        "Ticket.InvalidManager",
        "Người duyệt huỷ phải là Quản lý hoặc Chủ nhà hàng đang hoạt động.");
}
