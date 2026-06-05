using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Counters.ListCounters;

public static class ListCounters
{
    public sealed record Query(string? Search, bool? IsActive) : IQuery<IReadOnlyList<CounterItem>>;

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<CounterItem>>
    {
        public async Task<Result<IReadOnlyList<CounterItem>>> Handle(Query request, CancellationToken ct)
        {
            var q = dbContext.Counters.AsQueryable();

            if (request.IsActive.HasValue)
                q = q.Where(x => x.IsActive == request.IsActive.Value);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Name.ToLower().Contains(s)
                              || (x.Note != null && x.Note.ToLower().Contains(s)));
            }

            var rows = await q
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
                .Select(x => new CounterItem(
                    x.Id, x.Name, x.Note, x.DisplayOrder, x.IsActive, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<CounterItem>>(rows);
        }
    }
}
