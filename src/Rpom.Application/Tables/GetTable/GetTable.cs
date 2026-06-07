using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Tables.GetTable;

public static class GetTable
{
    public sealed record Query(int Id) : IQuery<Response>;

    public sealed record Response(
        int Id,
        int AreaId,
        string Code,
        int SeatCount,
        string? Description,
        string Status,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            Response? row = await dbContext.Tables
                .Where(x => x.Id == request.Id)
                .Select(x => new Response(
                    x.Id, x.AreaId, x.Code, x.SeatCount, x.Description, x.Status,
                    x.IsActive, x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Result.Failure<Response>(TableErrors.NotFound)
                : Result.Success(row);
        }
    }
}
