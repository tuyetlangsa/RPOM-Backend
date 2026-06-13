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
}
