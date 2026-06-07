using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Counters.GetCounter;

public static class GetCounter
{
    public sealed record Query(int Id) : IQuery<Response>;

    public sealed record Response(
        int Id,
        string Name,
        string? Note,
        short DisplayOrder,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            Response? row = await dbContext.Counters
                .Where(x => x.Id == request.Id)
                .Select(x => new Response(
                    x.Id, x.Name, x.Note, x.DisplayOrder, x.IsActive, x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Result.Failure<Response>(CounterErrors.NotFound)
                : Result.Success(row);
        }
    }
}
