using Rpom.Domain.Common;

namespace Rpom.Domain.Inventory;

public static class BomLineErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "BomLine.NotFound",
        "Không tìm thấy dòng công thức.");

    public static readonly Error DuplicateMaterial = Error.Conflict(
        "BomLine.DuplicateMaterial",
        "Nguyên liệu này đã có trong công thức.");

    public static readonly Error SellableItemNotFound = Error.NotFound(
        "BomLine.SellableItemNotFound",
        "Hàng hoá bán không tồn tại.");

    public static readonly Error MaterialItemNotFound = Error.NotFound(
        "BomLine.MaterialItemNotFound",
        "Nguyên liệu không tồn tại.");

    public static readonly Error SameItem = Error.BadRequest(
        "BomLine.SameItem",
        "Hàng hoá bán và nguyên liệu không thể giống nhau.");

    public static readonly Error MaterialMustBeStockable = Error.BadRequest(
        "BomLine.MaterialMustBeStockable",
        "Nguyên liệu phải là item có IsStockable.");

    public static readonly Error MaterialAlreadyHasRecipe = Error.Conflict(
        "BomLine.MaterialAlreadyHasRecipe",
        "Nguyên liệu này đã là SellableItem của 1 BOM khác — không được dùng làm nguyên liệu.");

    public static readonly Error InvalidMaterialUom = Error.BadRequest(
        "BomLine.InvalidMaterialUom",
        "Đơn vị tính phải là đơn vị cơ bản của nguyên liệu hoặc một quy đổi đã đăng ký.");
}
