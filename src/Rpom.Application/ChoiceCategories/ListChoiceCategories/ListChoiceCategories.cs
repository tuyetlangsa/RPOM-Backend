using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.ChoiceCategories.ListChoiceCategories;

public static class ListChoiceCategories
{
    public sealed record Query(string? Search, bool? IsActive) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        string Name,
        string? Note,
        short MinChoice,
        short? MaxChoice,
        short DisplayOrder,
        bool IsActive,
        int ModifierCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            var q = db.ChoiceCategories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim().ToLower();
                q = q.Where(c => c.Name.ToLower().Contains(s));
            }
            if (request.IsActive is { } active)
                q = q.Where(c => c.IsActive == active);

            var list = await q
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => new Response(
                    c.Id, c.Name, c.Note, c.MinChoice, c.MaxChoice, c.DisplayOrder, c.IsActive,
                    c.Modifiers.Count, c.CreatedAt, c.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(list);
        }
    }
}
