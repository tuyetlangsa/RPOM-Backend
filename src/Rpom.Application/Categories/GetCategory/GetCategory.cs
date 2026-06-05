using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.Categories.GetCategory;

public static class GetCategory
{
    public sealed record Query(int Id) : IQuery<Response>;

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

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var row = await dbContext.Categories
                .Where(x => x.Id == request.Id)
                .Select(x => new Response(
                    x.Id, x.Code, x.Name, x.Description, x.ParentId, x.Path, x.Level,
                    x.DisplayOrder, x.IsActive,
                    dbContext.ItemCategories.Count(ic => ic.CategoryId == x.Id),
                    dbContext.Categories.Count(c => c.ParentId == x.Id),
                    x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Result.Failure<Response>(CategoryErrors.NotFound)
                : Result.Success(row);
        }
    }
}
