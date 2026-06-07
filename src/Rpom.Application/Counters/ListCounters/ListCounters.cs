using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Counters.ListCounters;

public static class ListCounters
{
    public sealed record Query(string? Search, bool? IsActive) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        string Name,
        string? Note,
        short DisplayOrder,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            IQueryable<Counter> q = dbContext.Counters.AsQueryable();

            if (request.IsActive.HasValue)
            {
                q = q.Where(x => x.IsActive == request.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                string s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Name.ToLower().Contains(s)
                                 || (x.Note != null && x.Note.ToLower().Contains(s)));
            }

            List<Response> rows = await q
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
                .Select(x => new Response(
                    x.Id, x.Name, x.Note, x.DisplayOrder, x.IsActive, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(rows);
        }
    }
}
