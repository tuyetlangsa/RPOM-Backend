using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Inventory;

namespace Rpom.Application.Items.ListUomConversions;

public static class ListUomConversions
{
    public sealed record Query(int ItemId, bool? IsActive) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        int UomId,
        string UomCode,
        string UomName,
        decimal FactorToBase,
        bool IsActive);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            IQueryable<ItemUomConversion> q = dbContext.ItemUomConversions
                .Where(x => x.ItemId == request.ItemId);

            if (request.IsActive.HasValue)
            {
                q = q.Where(x => x.IsActive == request.IsActive.Value);
            }

            List<Response> rows = await q
                .OrderBy(x => x.Uom.Code)
                .Select(x => new Response(
                    x.Id, x.UomId, x.Uom.Code, x.Uom.Name, x.FactorToBase, x.IsActive))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(rows);
        }
    }
}
