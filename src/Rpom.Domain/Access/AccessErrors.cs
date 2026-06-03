using Rpom.Domain.Common;

namespace Rpom.Domain.Access;

public static class AccessErrors
{
    public static readonly Error InvalidCredentials = Error.BadRequest(
        "Access.InvalidCredentials",
        "Username hoặc mật khẩu không đúng.");

    public static readonly Error AccountInactive = new(
        "Access.AccountInactive",
        "Tài khoản đã bị vô hiệu hoá. Liên hệ chủ nhà hàng.",
        ErrorType.UnAuthorized);

    public static readonly Error AccountLocked = new(
        "Access.AccountLocked",
        "Tài khoản đang bị khoá. Liên hệ chủ nhà hàng để mở khoá.",
        ErrorType.UnAuthorized);

    public static readonly Error StaffNotFound = Error.NotFound(
        "Access.StaffNotFound",
        "Không tìm thấy tài khoản nhân viên.");
}
