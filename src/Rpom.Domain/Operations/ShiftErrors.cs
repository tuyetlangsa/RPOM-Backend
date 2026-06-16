using Rpom.Domain.Common;

namespace Rpom.Domain.Operations;

public static class ShiftErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Shift.NotFound",
        "Không tìm thấy ca làm việc.");

    public static readonly Error CodeDuplicate = Error.Conflict(
        "Shift.CodeDuplicate",
        "Mã ca làm việc đã tồn tại.");

    public static readonly Error InUse = Error.Conflict(
        "Shift.InUse",
        "Không xoá được vì ca làm việc đã được sử dụng trong lịch sử. Hãy đánh dấu không hoạt động thay vì xoá.");
}
