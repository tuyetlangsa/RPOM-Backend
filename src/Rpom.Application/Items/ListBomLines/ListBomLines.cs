using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Inventory;

namespace Rpom.Application.Items.ListBomLines;

public static class ListBomLines
{
    public sealed record Query(int ItemId, bool? IsActive) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        int MaterialItemId,
        string MaterialItemCode,
        string MaterialItemName,
        decimal Quantity,
        int UomId,
        string UomCode,
        string UomName,
        bool IsActive);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            IQueryable<BomLine> q = dbContext.BomLines
                .Where(x => x.SellableItemId == request.ItemId);

            if (request.IsActive.HasValue)
            {
                q = q.Where(x => x.IsActive == request.IsActive.Value);
            }

            List<Response> rows = await q
                .OrderBy(x => x.MaterialItem.Code)
                .Select(x => new Response(
                    x.Id,
                    x.MaterialItemId,
                    x.MaterialItem.Code,
                    x.MaterialItem.Name,
                    x.Quantity,
                    x.UomId,
                    x.Uom.Code,
                    x.Uom.Name,
                    x.IsActive))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(rows);
        }
    }
}
