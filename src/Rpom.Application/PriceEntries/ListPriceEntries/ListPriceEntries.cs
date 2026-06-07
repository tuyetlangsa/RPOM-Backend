using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.PriceEntries.ListPriceEntries;

public static class ListPriceEntries
{
    public sealed record Query(int PriceVariantId) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        int PriceVariantId,
        int ItemId,
        string ItemCode,
        string ItemName,
        decimal Price,
        bool IsVatIncluded,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            bool variantExists = await dbContext.PriceVariants.AnyAsync(v => v.Id == request.PriceVariantId, ct);
            if (!variantExists)
            {
                return Result.Failure<IReadOnlyList<Response>>(PriceEntryErrors.VariantNotFound);
            }

            var raw = await dbContext.PriceEntries
                .Where(e => e.PriceVariantId == request.PriceVariantId)
                .Join(dbContext.Items,
                    e => e.ItemId,
                    i => i.Id,
                    (e, i) => new
                    {
                        e.Id, e.PriceVariantId, e.ItemId,
                        ItemCode = i.Code, ItemName = i.Name,
                        e.Price, e.IsVatIncluded, e.CreatedAt, e.UpdatedAt
                    })
                .OrderBy(r => r.ItemName)
                .ToListAsync(ct);

            var rows = raw
                .Select(r => new Response(
                    r.Id, r.PriceVariantId, r.ItemId,
                    r.ItemCode, r.ItemName, r.Price, r.IsVatIncluded,
                    r.CreatedAt, r.UpdatedAt))
                .ToList();

            return Result.Success<IReadOnlyList<Response>>(rows);
        }
    }
}
