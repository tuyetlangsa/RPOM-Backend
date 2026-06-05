using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Tables.ListTables;

public static class ListTables
{
    public sealed record Query(int? CounterId, int? AreaId, string? Search, bool? IsActive)
        : IQuery<IReadOnlyList<TableItem>>;

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<TableItem>>
    {
        public async Task<Result<IReadOnlyList<TableItem>>> Handle(Query request, CancellationToken ct)
        {
            var q = dbContext.Tables.AsQueryable();
            if (request.AreaId.HasValue) q = q.Where(x => x.AreaId == request.AreaId.Value);
            if (request.IsActive.HasValue) q = q.Where(x => x.IsActive == request.IsActive.Value);

            if (request.CounterId.HasValue)
            {
                var areaIds = dbContext.Areas
                    .Where(a => a.CounterId == request.CounterId.Value)
                    .Select(a => a.Id);
                q = q.Where(x => areaIds.Contains(x.AreaId));
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Code.ToLower().Contains(s)
                              || (x.Description != null && x.Description.ToLower().Contains(s)));
            }

            var rows = await q
                .OrderBy(x => x.AreaId).ThenBy(x => x.Code)
                .Select(x => new TableItem(
                    x.Id, x.AreaId, x.Code, x.SeatCount, x.Description, x.Status,
                    x.IsActive, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<TableItem>>(rows);
        }
    }
}
