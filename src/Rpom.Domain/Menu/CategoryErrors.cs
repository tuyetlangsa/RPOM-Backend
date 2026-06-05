using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

public static class CategoryErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Category.NotFound",
        "Không tìm thấy nhóm hàng.");

    public static readonly Error CodeDuplicate = Error.Conflict(
        "Category.CodeDuplicate",
        "Mã nhóm hàng đã tồn tại.");

    public static readonly Error ParentNotFound = Error.NotFound(
        "Category.ParentNotFound",
        "Nhóm cha không tồn tại.");

    public static readonly Error ParentSelf = Error.BadRequest(
        "Category.ParentSelf",
        "Một nhóm không thể là cha của chính nó.");

    public static readonly Error ParentCycle = Error.BadRequest(
        "Category.ParentCycle",
        "Đổi cha sẽ tạo vòng lặp — không hợp lệ. Bạn đang gán cha là một nhóm con của chính nó.");

    public static readonly Error InUseByItems = Error.Conflict(
        "Category.InUseByItems",
        "Không xoá được — đang có hàng hoá thuộc nhóm này. Hãy chuyển hàng hoá sang nhóm khác trước.");

    public static readonly Error InUseByChildren = Error.Conflict(
        "Category.InUseByChildren",
        "Không xoá được — nhóm này còn nhóm con. Hãy xoá hoặc chuyển nhóm con trước.");
}
