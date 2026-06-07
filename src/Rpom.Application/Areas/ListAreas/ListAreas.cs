using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Areas.ListAreas;

public static class ListAreas
{
    public sealed record Query(int? CounterId, string? Search, bool? IsActive)
        : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        int CounterId,
        string Name,
        string? Description,
        short DisplayOrder,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            IQueryable<Area> q = dbContext.Areas.AsQueryable();
            if (request.CounterId.HasValue)
            {
                q = q.Where(x => x.CounterId == request.CounterId.Value);
            }

            if (request.IsActive.HasValue)
            {
                q = q.Where(x => x.IsActive == request.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                string s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Name.ToLower().Contains(s)
                                 || (x.Description != null && x.Description.ToLower().Contains(s)));
            }

            List<Response> rows = await q
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
                .Select(x => new Response(
                    x.Id, x.CounterId, x.Name, x.Description, x.DisplayOrder,
                    x.IsActive, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(rows);
        }
    }
}
