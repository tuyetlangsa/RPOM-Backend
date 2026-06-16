using Rpom.Domain.Common;

namespace Rpom.Domain.Inventory;

public static class ItemUomConversionErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "ItemUomConversion.NotFound",
        "Không tìm thấy quy đổi đơn vị tính.");

    public static readonly Error DuplicateUom = Error.Conflict(
        "ItemUomConversion.DuplicateUom",
        "Quy đổi cho đơn vị này đã tồn tại.");
}
