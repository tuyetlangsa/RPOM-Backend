using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

public static class SetMenuErrors
{
    public static readonly Error NotASetMenu = Error.NotFound(
        "SetMenu.NotASetMenu",
        "Mặt hàng này không phải Set menu.");

    public static readonly Error ComponentNotFound = Error.NotFound(
        "SetMenu.ComponentNotFound",
        "Một hoặc nhiều Mặt hàng thành phần không tồn tại.");

    public static readonly Error SelfComponent = Error.BadRequest(
        "SetMenu.SelfComponent",
        "Set menu không thể chứa chính nó làm thành phần.");

    public static readonly Error ChoiceCategoryNotFound = Error.NotFound(
        "SetMenu.ChoiceCategoryNotFound",
        "Một hoặc nhiều Nhóm lựa chọn không tồn tại hoặc đã ngừng dùng.");
}
