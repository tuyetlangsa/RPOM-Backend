using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Lookups.GetShifts;

public static class GetShifts
{
    public sealed record Query : IQuery<IReadOnlyList<ShiftItem>>;

    public sealed record ShiftItem(
        int Id,
        string Code,
        string Name,
        TimeOnly BeginTime,
        TimeOnly EndTime,
        bool IsNextDay);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<ShiftItem>>
    {
        public async Task<Result<IReadOnlyList<ShiftItem>>> Handle(Query request, CancellationToken ct)
        {
            List<ShiftItem> rows = await dbContext.Shifts
                .Where(x => x.IsActive)
                .OrderBy(x => x.BeginTime)
                .Select(x => new ShiftItem(x.Id, x.Code, x.Name, x.BeginTime, x.EndTime, x.IsNextDay))
                .ToListAsync(ct);
            return Result.Success<IReadOnlyList<ShiftItem>>(rows);
        }
    }
}
