using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.PriceVariants.GetPriceVariant;

public static class GetPriceVariant
{
    public sealed record Query(int Id) : IQuery<Response>;

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

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var v = await dbContext.PriceVariants
                .Where(x => x.Id == request.Id)
                .Select(x => new
                {
                    x.Id, x.PriceTableId, x.Code, x.Name, x.Description,
                    x.BeginTime, x.EndTime, x.DayMask, x.AppliesToAllAreas,
                    x.IsActive, x.CreatedAt, x.UpdatedAt,
                    AreaIds = dbContext.PriceVariantAreas
                        .Where(a => a.PriceVariantId == x.Id)
                        .Select(a => a.AreaId).ToList(),
                    EntryCount = dbContext.PriceEntries.Count(e => e.PriceVariantId == x.Id),
                })
                .FirstOrDefaultAsync(ct);

            if (v is null) return Result.Failure<Response>(PriceVariantErrors.NotFound);

            var spec =
                (v.BeginTime is not null || v.EndTime is not null ? 1 : 0)
              + (v.DayMask is not null ? 1 : 0)
              + (v.AppliesToAllAreas ? 0 : 1);

            return Result.Success(new Response(
                v.Id, v.PriceTableId, v.Code, v.Name, v.Description,
                v.BeginTime, v.EndTime, v.DayMask, v.AppliesToAllAreas,
                v.AreaIds, spec, v.IsActive, v.EntryCount,
                v.CreatedAt, v.UpdatedAt));
        }
    }
}
