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

    public static readonly Error PairingCodeInvalid = Error.NotFound(
        "CustomerDisplay.PairingCodeInvalid",
        "Mã ghép không đúng hoặc màn hình không khả dụng.");

    public static readonly Error CounterNotOpen = Error.Conflict(
        "CustomerDisplay.CounterNotOpen",
        "Quầy của màn hình này chưa mở ca — không thể ghép.");

    public static readonly Error NotPaired = Error.NotFound(
        "CustomerDisplay.NotPaired",
        "Bạn chưa ghép với màn hình khách nào.");
}
