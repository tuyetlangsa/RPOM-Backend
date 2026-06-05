namespace Rpom.Application.Uoms;

public sealed record UomItem(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
