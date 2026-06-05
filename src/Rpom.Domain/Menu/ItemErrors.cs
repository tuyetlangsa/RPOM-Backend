using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

public static class ItemErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Item.NotFound",
        "Không tìm thấy hàng hoá.");

    public static readonly Error CodeDuplicate = Error.Conflict(
        "Item.CodeDuplicate",
        "Mã hàng hoá đã tồn tại.");

    public static readonly Error UomNotFound = Error.NotFound(
        "Item.UomNotFound",
        "Đơn vị tính được tham chiếu không tồn tại.");

    public static readonly Error KitchenStationNotFound = Error.NotFound(
        "Item.KitchenStationNotFound",
        "Bếp được tham chiếu không tồn tại.");

    public static readonly Error CategoriesRequired = Error.BadRequest(
        "Item.CategoriesRequired",
        "Hàng hoá phải thuộc ít nhất 1 nhóm.");

    public static readonly Error PrimaryCategoryRequired = Error.BadRequest(
        "Item.PrimaryCategoryRequired",
        "Phải chỉ định đúng 1 nhóm chính (primary category).");

    public static readonly Error CategoryNotFound = Error.NotFound(
        "Item.CategoryNotFound",
        "Một nhóm được tham chiếu không tồn tại.");

    public static readonly Error InUse = Error.Conflict(
        "Item.InUse",
        "Không xoá được — hàng hoá đang được sử dụng (đơn hàng, bảng giá, công thức, hoặc set menu). Hãy tắt kích hoạt thay vì xoá.");
}
