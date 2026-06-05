using Rpom.Domain.Common;

namespace Rpom.Domain.Restaurant;

public static class TableErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Table.NotFound",
        "Không tìm thấy Bàn.");

    public static readonly Error AreaNotFound = Error.NotFound(
        "Table.AreaNotFound",
        "Khu được tham chiếu không tồn tại.");

    public static readonly Error CodeRequired = Error.BadRequest(
        "Table.CodeRequired",
        "Mã Bàn là bắt buộc.");

    public static readonly Error CodeDuplicateInArea = Error.Conflict(
        "Table.CodeDuplicateInArea",
        "Mã Bàn đã tồn tại trong Khu này.");

    public static readonly Error CannotDeleteOccupied = Error.Conflict(
        "Table.CannotDeleteOccupied",
        "Không xoá được Bàn đang phục vụ.");
}
