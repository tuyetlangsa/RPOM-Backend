using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.CancellationReasons.ListCancellationReasons;

public static class ListCancellationReasons
{
    public sealed record Query(string? Search, bool? IsActive) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        short DisplayOrder,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            IQueryable<CancellationReason> q = dbContext.CancellationReasons.AsQueryable();

            if (request.IsActive.HasValue)
            {
                q = q.Where(x => x.IsActive == request.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                string s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Code.ToLower().Contains(s)
                                 || x.Name.ToLower().Contains(s));
            }

            List<Response> rows = await q
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new Response(
                    x.Id, x.Code, x.Name, x.DisplayOrder, x.IsActive, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(rows);
        }
    }
}
