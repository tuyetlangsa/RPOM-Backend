using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

public static class ShiftSessionErrors
{
    public static readonly Error AlreadyOpen = Error.Conflict(
        "ShiftSession.AlreadyOpen",
        "Bạn đã có ca đang mở. Hãy đóng ca hiện tại trước.");

    public static readonly Error CounterCashierOccupied = Error.Conflict(
        "ShiftSession.CounterCashierOccupied",
        "Đã có thu ngân khác đang mở ca tại quầy này. Mỗi quầy chỉ 1 cashier 1 thời điểm.");

    public static readonly Error NotFound = Error.NotFound(
        "ShiftSession.NotFound",
        "Không tìm thấy ca này.");

    public static readonly Error NotOwner = new(
        "ShiftSession.NotOwner",
        "Bạn chỉ được đóng ca của chính mình.",
        ErrorType.UnAuthorized);

    public static readonly Error NotOpen = Error.Conflict(
        "ShiftSession.NotOpen",
        "Ca này đã đóng — không thể đóng lại.");

    public static readonly Error ShiftDefinitionInvalid = Error.BadRequest(
        "ShiftSession.ShiftDefinitionInvalid",
        "Định nghĩa ca không tồn tại hoặc đã ngừng hoạt động.");

    public static readonly Error CounterInvalid = Error.BadRequest(
        "ShiftSession.CounterInvalid",
        "Quầy không tồn tại hoặc đã ngừng hoạt động.");

    public static readonly Error KitchenStationInvalid = Error.BadRequest(
        "ShiftSession.KitchenStationInvalid",
        "Bếp không tồn tại hoặc đã ngừng hoạt động.");

    public static readonly Error ScopeRequiredCounter = Error.BadRequest(
        "ShiftSession.ScopeRequiredCounter",
        "Role hiện tại yêu cầu chọn Quầy (Counter) để mở ca.");

    public static readonly Error ScopeRequiredKitchenStation = Error.BadRequest(
        "ShiftSession.ScopeRequiredKitchenStation",
        "Kitchen Staff yêu cầu chọn Bếp (KitchenStation) để mở ca.");

    public static readonly Error CashierMustTrackCash = Error.BadRequest(
        "ShiftSession.CashierMustTrackCash",
        "Cashier bắt buộc đếm tiền mặt khi mở ca (HasCashTracking phải là true).");

    public static readonly Error NonCashierCannotTrackCash = Error.BadRequest(
        "ShiftSession.NonCashierCannotTrackCash",
        "Chỉ Cashier mới được tracking tiền mặt.");

    public static readonly Error CashCountsRequired = Error.BadRequest(
        "ShiftSession.CashCountsRequired",
        "Cần nhập số lượng từng mệnh giá khi tracking tiền mặt.");

    public static readonly Error DenominationInvalid = Error.BadRequest(
        "ShiftSession.DenominationInvalid",
        "Một số mệnh giá không tồn tại hoặc đã tắt.");
}
