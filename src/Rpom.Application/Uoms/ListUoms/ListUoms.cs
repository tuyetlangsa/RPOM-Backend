using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Uoms.ListUoms;

public static class ListUoms
{
    public sealed record Query(string? Search, bool? IsActive) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
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
                .Select(x => new Response(
                    x.Id, x.Code, x.Name, x.Description, x.IsActive, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(rows);
        }
    }
}
