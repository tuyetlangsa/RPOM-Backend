using Rpom.Domain.Common;

namespace Rpom.Domain.Sales.CashDrawer;

public static class CashDrawerErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "CashDrawer.NotFound",
        "Không tìm thấy phiên quầy này.");

    public static readonly Error CounterAlreadyOpen = Error.Conflict(
        "CashDrawer.CounterAlreadyOpen",
        "Quầy này đã có phiên đang mở — không thể mở thêm. Hãy đóng phiên hiện tại trước.");

    public static readonly Error CounterInvalid = Error.BadRequest(
        "CashDrawer.CounterInvalid",
        "Quầy không tồn tại hoặc đã ngừng hoạt động.");

    public static readonly Error CashCountsRequired = Error.BadRequest(
        "CashDrawer.CashCountsRequired",
        "Phải nhập đếm tiền (mệnh giá + số lượng) khi mở hoặc đóng phiên.");

    public static readonly Error DenominationInvalid = Error.BadRequest(
        "CashDrawer.DenominationInvalid",
        "Một số mệnh giá không tồn tại hoặc đã tắt.");

    public static readonly Error ShiftInvalid = Error.BadRequest(
        "CashDrawer.ShiftInvalid",
        "Ca làm việc không tồn tại hoặc đã ngừng hoạt động.");

    public static readonly Error NotOpen = Error.Conflict(
        "CashDrawer.NotOpen",
        "Phiên quầy này đã đóng — không thể thao tác tiếp.");
}
