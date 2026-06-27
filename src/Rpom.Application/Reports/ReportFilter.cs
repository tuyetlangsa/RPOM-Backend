namespace Rpom.Application.Reports;

/// <summary>
///     Shared filter parameters for all report queries.
///     Only input params — not a response DTO, so sharing is fine per CLAUDE.md §2.
/// </summary>
public sealed record ReportFilter(
    DateTime? FromDate,
    DateTime? ToDate,
    int? CounterId,
    int? AreaId,
    int? ShiftId,
    int? CategoryId);
