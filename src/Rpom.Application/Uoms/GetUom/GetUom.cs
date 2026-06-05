using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.Uoms.GetUom;

public static class GetUom
{
    public sealed record Query(int Id) : IQuery<UomItem>;

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, UomItem>
    {
        public async Task<Result<UomItem>> Handle(Query request, CancellationToken ct)
        {
            var row = await dbContext.Uoms
                .Where(x => x.Id == request.Id)
                .Select(x => new UomItem(
                    x.Id, x.Code, x.Name, x.Description, x.IsActive, x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Result.Failure<UomItem>(UomErrors.NotFound)
                : Result.Success(row);
        }
    }
}
