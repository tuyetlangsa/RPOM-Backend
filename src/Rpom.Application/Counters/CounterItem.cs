namespace Rpom.Application.Counters;

/// <summary>
/// DTO returned by every Counter endpoint. Mirrors backend Domain entity
/// 1:1 — frontend `types/api/restaurant.ts#Counter` consumes this shape.
/// </summary>
public sealed record CounterItem(
    int Id,
    string Name,
    string? Note,
    short DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
