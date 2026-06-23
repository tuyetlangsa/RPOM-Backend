using Rpom.Domain.Common;

namespace Rpom.Domain.Operations;

public static class CustomerDisplayErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "CustomerDisplay.NotFound",
        "Không tìm thấy màn hình khách.");

    public static readonly Error InvalidToken = Error.NotFound(
        "CustomerDisplay.InvalidToken",
        "Thiết bị màn hình khách không hợp lệ.");

    public static readonly Error TerminalAlreadyLinked = Error.Conflict(
        "CustomerDisplay.TerminalAlreadyLinked",
        "Máy POS này đã được gắn với một màn hình khách khác. Hãy inactivate màn cũ rồi đăng ký lại.");

    public static readonly Error AlreadyActivated = Error.Conflict(
        "CustomerDisplay.AlreadyActivated",
        "Token này đã được kích hoạt trên một máy khách khác — không dùng được trên máy này.");

    public static readonly Error NotActivated = Error.Conflict(
        "CustomerDisplay.NotActivated",
        "Thiết bị chưa được kích hoạt hoặc không khớp máy đã kích hoạt.");
}
