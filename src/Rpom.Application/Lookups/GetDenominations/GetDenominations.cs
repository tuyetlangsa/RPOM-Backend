using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Lookups.GetDenominations;

public static class GetDenominations
{
    public sealed record Query : IQuery<IReadOnlyList<DenominationItem>>;

    public sealed record DenominationItem(int Id, decimal FaceValue, string Name, short DisplayOrder);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<DenominationItem>>
    {
        public async Task<Result<IReadOnlyList<DenominationItem>>> Handle(Query request, CancellationToken ct)
        {
            List<DenominationItem> rows = await dbContext.Denominations
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.FaceValue)
                .Select(x => new DenominationItem(x.Id, x.FaceValue, x.Name, x.DisplayOrder))
                .ToListAsync(ct);
            return Result.Success<IReadOnlyList<DenominationItem>>(rows);
        }
    }
}
