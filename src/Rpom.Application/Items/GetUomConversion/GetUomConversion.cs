using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Inventory;

namespace Rpom.Application.Items.GetUomConversion;

public static class GetUomConversion
{
    public sealed record Query(int ItemId, int Id) : IQuery<Response>;

    public sealed record Response(
        int Id,
        int ItemId,
        int UomId,
        string UomCode,
        string UomName,
        decimal FactorToBase,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            Response? row = await dbContext.ItemUomConversions
                .Where(x => x.Id == request.Id && x.ItemId == request.ItemId)
                .Select(x => new Response(
                    x.Id, x.ItemId, x.UomId, x.Uom.Code, x.Uom.Name,
                    x.FactorToBase, x.IsActive, x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Result.Failure<Response>(ItemUomConversionErrors.NotFound)
                : Result.Success(row);
        }
    }
}
