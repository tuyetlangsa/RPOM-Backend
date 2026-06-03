using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Lookups.GetCounters;

public static class GetCounters
{
    public sealed record Query : IQuery<IReadOnlyList<CounterItem>>;

    public sealed record CounterItem(int Id, string Name, short DisplayOrder);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<CounterItem>>
    {
        public async Task<Result<IReadOnlyList<CounterItem>>> Handle(Query request, CancellationToken ct)
        {
            var rows = await dbContext.Counters
                .Where(x => x.IsActive)
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
                .Select(x => new CounterItem(x.Id, x.Name, x.DisplayOrder))
                .ToListAsync(ct);
            return Result.Success<IReadOnlyList<CounterItem>>(rows);
        }
    }
}
