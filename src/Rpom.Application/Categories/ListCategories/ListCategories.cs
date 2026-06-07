using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.Categories.ListCategories;

public static class ListCategories
{
    public sealed record Query(string? Search, bool? IsActive, string? RootCode) : IQuery<IReadOnlyList<Response>>;

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
            IQueryable<Category> q = dbContext.Categories.AsQueryable();
            if (request.IsActive.HasValue)
            {
                q = q.Where(x => x.IsActive == request.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                string s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Code.ToLower().Contains(s) || x.Name.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(request.RootCode))
            {
                // Trả về subtree (self + descendants) của Category root có Code khớp.
                // Path semicolon-terminated → StartsWith(root.Path) phủ cả self và mọi descendant.
                string rootCodeLower = request.RootCode.Trim().ToLower();
                var root = await dbContext.Categories
                    .Where(c => c.Code.ToLower() == rootCodeLower)
                    .Select(c => new { c.Path })
                    .FirstOrDefaultAsync(ct);
                if (root is null)
                {
                    return Result.Success<IReadOnlyList<Response>>(Array.Empty<Response>());
                }

                q = q.Where(c => c.Path.StartsWith(root.Path));
            }

            // Pre-compute child + item counts in one round-trip to avoid N+1.
            List<Response> rows = await q
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
