using Rpom.Domain.Common;

namespace Rpom.Domain.Menu;

public static class UomErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Uom.NotFound",
        "Không tìm thấy đơn vị tính.");

    public static readonly Error CodeDuplicate = Error.Conflict(
        "Uom.CodeDuplicate",
        "Mã đơn vị tính đã tồn tại.");

    public static readonly Error InUse = Error.Conflict(
        "Uom.InUse",
        "Không xoá được vì đơn vị tính đang được sử dụng bởi Hàng hoá. Hãy đổi đơn vị tính của các Hàng hoá trước.");
}
