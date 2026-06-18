using Rpom.Domain.Common;

namespace Rpom.Domain.Operations;

public static class KitchenStationErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "KitchenStation.NotFound",
        "Không tìm thấy bếp.");

    public static readonly Error NotSelected = Error.BadRequest(
        "KitchenStation.NotSelected",
        "Phiên đăng nhập chưa chọn khu bếp. Hãy chọn khu bếp trước khi vào màn hình bếp.");

    public static readonly Error CodeDuplicate = Error.Conflict(
        "KitchenStation.CodeDuplicate",
        "Mã bếp đã tồn tại.");

    public static readonly Error InUse = Error.Conflict(
        "KitchenStation.InUse",
        "Không xoá được vì bếp đang được sử dụng bởi Hàng hoá. Hãy đổi bếp của các Hàng hoá trước.");
}
