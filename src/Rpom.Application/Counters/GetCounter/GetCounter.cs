using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Counters.GetCounter;

public static class GetCounter
{
    public sealed record Query(int Id) : IQuery<CounterItem>;

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, CounterItem>
    {
        public async Task<Result<CounterItem>> Handle(Query request, CancellationToken ct)
        {
            var row = await dbContext.Counters
                .Where(x => x.Id == request.Id)
                .Select(x => new CounterItem(
                    x.Id, x.Name, x.Note, x.DisplayOrder, x.IsActive, x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Result.Failure<CounterItem>(CounterErrors.NotFound)
                : Result.Success(row);
        }
    }
}
