using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

public static class CancellationReasonErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "CancellationReason.NotFound",
        "Không tìm thấy lý do huỷ.");

    public static readonly Error CodeDuplicate = Error.Conflict(
        "CancellationReason.CodeDuplicate",
        "Mã lý do huỷ đã tồn tại.");

    public static readonly Error InUse = Error.Conflict(
        "CancellationReason.InUse",
        "Không xoá được vì lý do huỷ đã được sử dụng trong lịch sử. Hãy đánh dấu không hoạt động thay vì xoá.");
}
