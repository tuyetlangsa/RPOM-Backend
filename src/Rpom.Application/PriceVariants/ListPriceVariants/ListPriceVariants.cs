using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.PriceVariants.ListPriceVariants;

public static class ListPriceVariants
{
    public sealed record Query(int PriceTableId) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        int PriceTableId,
        string Code,
        string Name,
        string? Description,
        TimeOnly? BeginTime,
        TimeOnly? EndTime,
        int? DayMask,
        bool AppliesToAllAreas,
        IReadOnlyList<int> AreaIds,
        int Specificity,
        bool IsActive,
        int EntryCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            var raw = await dbContext.PriceVariants
                .Where(v => v.PriceTableId == request.PriceTableId)
                .OrderByDescending(v =>
                    (v.BeginTime != null || v.EndTime != null ? 1 : 0)
                  + (v.DayMask != null ? 1 : 0)
                  + (v.AppliesToAllAreas ? 0 : 1))
                .ThenBy(v => v.Code)
                .Select(v => new
                {
                    v.Id, v.PriceTableId, v.Code, v.Name, v.Description,
                    v.BeginTime, v.EndTime, v.DayMask, v.AppliesToAllAreas,
                    v.IsActive, v.CreatedAt, v.UpdatedAt,
                    AreaIds = dbContext.PriceVariantAreas
                        .Where(a => a.PriceVariantId == v.Id)
                        .Select(a => a.AreaId).ToList(),
                    EntryCount = dbContext.PriceEntries.Count(e => e.PriceVariantId == v.Id),
                })
                .ToListAsync(ct);

            var rows = raw.Select(v =>
            {
                var spec =
                    (v.BeginTime is not null || v.EndTime is not null ? 1 : 0)
                  + (v.DayMask is not null ? 1 : 0)
                  + (v.AppliesToAllAreas ? 0 : 1);
                return new Response(
                    v.Id, v.PriceTableId, v.Code, v.Name, v.Description,
                    v.BeginTime, v.EndTime, v.DayMask, v.AppliesToAllAreas,
                    v.AreaIds, spec, v.IsActive, v.EntryCount,
                    v.CreatedAt, v.UpdatedAt);
            }).ToList();

            return Result.Success<IReadOnlyList<Response>>(rows);
        }
    }
}
