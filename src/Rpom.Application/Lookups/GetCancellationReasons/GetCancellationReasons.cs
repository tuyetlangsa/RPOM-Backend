using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Lookups.GetCancellationReasons;

public static class GetCancellationReasons
{
    public sealed record Query : IQuery<IReadOnlyList<CancellationReasonItem>>;

    public sealed record CancellationReasonItem(
        int Id,
        string Code,
        string Name,
        short DisplayOrder);

    internal sealed class Handler(IDbContext dbContext)
        : IQueryHandler<Query, IReadOnlyList<CancellationReasonItem>>
    {
        public async Task<Result<IReadOnlyList<CancellationReasonItem>>> Handle(
            Query request, CancellationToken ct)
        {
            List<CancellationReasonItem> rows = await dbContext.CancellationReasons
                .Where(x => x.IsActive)
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new CancellationReasonItem(x.Id, x.Code, x.Name, x.DisplayOrder))
                .ToListAsync(ct);
            return Result.Success<IReadOnlyList<CancellationReasonItem>>(rows);
        }
    }
}
