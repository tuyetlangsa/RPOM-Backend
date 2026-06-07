using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.PriceTables.ListPriceTables;

public static class ListPriceTables
{
    public sealed record Query(
        string? Search,
        bool? IsActive,
        DateOnly? BeginDateFrom,
        DateOnly? BeginDateTo) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
        DateOnly? BeginDate,
        DateOnly? EndDate,
        bool IsActive,
        int VariantCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            IQueryable<PriceTable> q = dbContext.PriceTables.AsQueryable();
            if (request.IsActive.HasValue)
            {
                q = q.Where(x => x.IsActive == request.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                string s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Code.ToLower().Contains(s) || x.Name.ToLower().Contains(s));
            }

            if (request.BeginDateFrom.HasValue)
            {
                q = q.Where(x => x.BeginDate != null && x.BeginDate >= request.BeginDateFrom.Value);
            }

            if (request.BeginDateTo.HasValue)
            {
                q = q.Where(x => x.BeginDate != null && x.BeginDate <= request.BeginDateTo.Value);
            }

            List<Response> rows = await q
                .OrderByDescending(x => x.IsActive).ThenBy(x => x.Code)
                .Select(x => new Response(
                    x.Id, x.Code, x.Name, x.Description, x.BeginDate, x.EndDate, x.IsActive,
                    dbContext.PriceVariants.Count(v => v.PriceTableId == x.Id),
                    x.CreatedAt, x.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(rows);
        }
    }
}
