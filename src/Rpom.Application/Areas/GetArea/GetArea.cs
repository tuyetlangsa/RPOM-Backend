using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Areas.GetArea;

public static class GetArea
{
    public sealed record Query(int Id) : IQuery<Response>;

    public sealed record Response(
        int Id,
        int CounterId,
        string Name,
        string? Description,
        short DisplayOrder,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var row = await dbContext.Areas
                .Where(x => x.Id == request.Id)
                .Select(x => new Response(
                    x.Id, x.CounterId, x.Name, x.Description, x.DisplayOrder,
                    x.IsActive, x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Result.Failure<Response>(AreaErrors.NotFound)
                : Result.Success(row);
        }
    }
}
