using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Lookups.GetKitchenStations;

public static class GetKitchenStations
{
    public sealed record Query : IQuery<IReadOnlyList<KitchenStationItem>>;

    public sealed record KitchenStationItem(int Id, string Code, string Name, short DisplayOrder);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<KitchenStationItem>>
    {
        public async Task<Result<IReadOnlyList<KitchenStationItem>>> Handle(Query request, CancellationToken ct)
        {
            var rows = await dbContext.KitchenStations
                .Where(x => x.IsActive)
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
                .Select(x => new KitchenStationItem(x.Id, x.Code, x.Name, x.DisplayOrder))
                .ToListAsync(ct);
            return Result.Success<IReadOnlyList<KitchenStationItem>>(rows);
        }
    }
}
