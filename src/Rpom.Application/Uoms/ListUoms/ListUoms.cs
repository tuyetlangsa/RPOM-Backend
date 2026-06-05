using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Uoms.ListUoms;

public static class ListUoms
{
    public sealed record Query(string? Search, bool? IsActive) : IQuery<IReadOnlyList<UomItem>>;

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<UomItem>>
    {
        public async Task<Result<IReadOnlyList<UomItem>>> Handle(Query request, CancellationToken ct)
        {
            var q = dbContext.Uoms.AsQueryable();

            if (request.IsActive.HasValue)
                q = q.Where(x => x.IsActive == request.IsActive.Value);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Code.ToLower().Contains(s)
                              || x.Name.ToLower().Contains(s)
                              || (x.Description != null && x.Description.ToLower().Contains(s)));
            }

            var rows = await q
                .OrderBy(x => x.Code)
                .Select(x => new UomItem(
                    x.Id, x.Code, x.Name, x.Description, x.IsActive, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<UomItem>>(rows);
        }
    }
}
