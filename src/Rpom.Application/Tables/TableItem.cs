namespace Rpom.Application.Tables;

public sealed record TableItem(
    int Id,
    int AreaId,
    string Code,
    int SeatCount,
    string? Description,
    string Status,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
