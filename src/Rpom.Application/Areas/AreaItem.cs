namespace Rpom.Application.Areas;

public sealed record AreaItem(
    int Id,
    int CounterId,
    string Name,
    string? Description,
    short DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
