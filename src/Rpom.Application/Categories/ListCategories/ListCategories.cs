using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Categories.ListCategories;

public static class ListCategories
{
    public sealed record Query(string? Search, bool? IsActive) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
        int? ParentId,
        string Path,
        short Level,
        short DisplayOrder,
        bool IsActive,
        int ItemCount,
        int ChildCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            var q = dbContext.Categories.AsQueryable();
            if (request.IsActive.HasValue) q = q.Where(x => x.IsActive == request.IsActive.Value);
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Code.ToLower().Contains(s) || x.Name.ToLower().Contains(s));
            }

            // Pre-compute child + item counts in one round-trip to avoid N+1.
            var rows = await q
                .OrderBy(x => x.Level).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Name)
                .Select(x => new Response(
                    x.Id, x.Code, x.Name, x.Description, x.ParentId, x.Path, x.Level,
                    x.DisplayOrder, x.IsActive,
                    dbContext.ItemCategories.Count(ic => ic.CategoryId == x.Id),
                    dbContext.Categories.Count(c => c.ParentId == x.Id),
                    x.CreatedAt, x.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(rows);
        }
    }
}
