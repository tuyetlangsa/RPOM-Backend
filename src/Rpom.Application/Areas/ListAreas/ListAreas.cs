using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Areas.ListAreas;

public static class ListAreas
{
    public sealed record Query(int? CounterId, string? Search, bool? IsActive)
        : IQuery<IReadOnlyList<AreaItem>>;

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<AreaItem>>
    {
        public async Task<Result<IReadOnlyList<AreaItem>>> Handle(Query request, CancellationToken ct)
        {
            var q = dbContext.Areas.AsQueryable();
            if (request.CounterId.HasValue) q = q.Where(x => x.CounterId == request.CounterId.Value);
            if (request.IsActive.HasValue) q = q.Where(x => x.IsActive == request.IsActive.Value);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Name.ToLower().Contains(s)
                              || (x.Description != null && x.Description.ToLower().Contains(s)));
            }

            var rows = await q
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
                .Select(x => new AreaItem(
                    x.Id, x.CounterId, x.Name, x.Description, x.DisplayOrder,
                    x.IsActive, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<AreaItem>>(rows);
        }
    }
}
