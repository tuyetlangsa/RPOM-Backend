using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Inventory;

namespace Rpom.Application.Items.GetBomLine;

public static class GetBomLine
{
    public sealed record Query(int ItemId, int Id) : IQuery<Response>;

    public sealed record Response(
        int Id,
        int SellableItemId,
        int MaterialItemId,
        string MaterialItemCode,
        string MaterialItemName,
        decimal Quantity,
        int UomId,
        string UomCode,
        string UomName,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            Response? row = await dbContext.BomLines
                .Where(x => x.Id == request.Id && x.SellableItemId == request.ItemId)
                .Select(x => new Response(
                    x.Id,
                    x.SellableItemId,
                    x.MaterialItemId,
                    x.MaterialItem.Code,
                    x.MaterialItem.Name,
                    x.Quantity,
                    x.UomId,
                    x.Uom.Code,
                    x.Uom.Name,
                    x.IsActive,
                    x.CreatedAt,
                    x.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Result.Failure<Response>(BomLineErrors.NotFound)
                : Result.Success(row);
        }
    }
}
