using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Tables.GetTable;

public static class GetTable
{
    public sealed record Query(int Id) : IQuery<TableItem>;

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, TableItem>
    {
        public async Task<Result<TableItem>> Handle(Query request, CancellationToken ct)
        {
            var row = await dbContext.Tables
                .Where(x => x.Id == request.Id)
                .Select(x => new TableItem(
                    x.Id, x.AreaId, x.Code, x.SeatCount, x.Description, x.Status,
                    x.IsActive, x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Result.Failure<TableItem>(TableErrors.NotFound)
                : Result.Success(row);
        }
    }
}
