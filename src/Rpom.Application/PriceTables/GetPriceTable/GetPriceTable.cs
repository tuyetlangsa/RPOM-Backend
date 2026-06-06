using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.PriceTables.GetPriceTable;

public static class GetPriceTable
{
    public sealed record Query(int Id) : IQuery<Response>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
        DateOnly? BeginDate,
        DateOnly? EndDate,
        bool IsActive,
        int VariantCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var row = await dbContext.PriceTables
                .Where(x => x.Id == request.Id)
                .Select(x => new Response(
                    x.Id, x.Code, x.Name, x.Description, x.BeginDate, x.EndDate, x.IsActive,
                    dbContext.PriceVariants.Count(v => v.PriceTableId == x.Id),
                    x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Result.Failure<Response>(PriceTableErrors.NotFound)
                : Result.Success(row);
        }
    }
}
