using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.Uoms.GetUom;

public static class GetUom
{
    public sealed record Query(int Id) : IQuery<Response>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            Response? row = await dbContext.Uoms
                .Where(x => x.Id == request.Id)
                .Select(x => new Response(
                    x.Id, x.Code, x.Name, x.Description, x.IsActive, x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Result.Failure<Response>(UomErrors.NotFound)
                : Result.Success(row);
        }
    }
}
