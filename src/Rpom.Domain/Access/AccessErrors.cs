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

    public static readonly Error UnknownPageCode = Error.BadRequest(
        "Access.UnknownPageCode",
        "Mã trang không hợp lệ.");

    public static readonly Error UsernameDuplicate = Error.Conflict(
        "Access.UsernameDuplicate",
        "Tên đăng nhập đã tồn tại.");

    public static readonly Error RoleNotFound = Error.NotFound(
        "Access.RoleNotFound",
        "Không tìm thấy vai trò.");

    public static readonly Error UnknownPermissionCode = Error.BadRequest(
        "Access.UnknownPermissionCode",
        "Mã quyền không hợp lệ.");
}
