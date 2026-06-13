using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Configuration.GetConfigValues;

public static class GetConfigValues
{
    public sealed record Query : IQuery<IReadOnlyList<ConfigValueItem>>;

    public sealed record ConfigValueItem(
        string Code,
        string? Value,
        string ValueType,
        string? Description,
        DateTime UpdatedAt,
        int? UpdatedByStaffAccountId);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, IReadOnlyList<ConfigValueItem>>
    {
        public async Task<Result<IReadOnlyList<ConfigValueItem>>> Handle(Query request, CancellationToken ct)
        {
            List<ConfigValueItem> rows = await dbContext.ConfigValues
                .OrderBy(x => x.Code)
                .Select(x => new ConfigValueItem(
                    x.Code, x.Value, x.ValueType, x.Description, x.UpdatedAt, x.UpdatedByStaffAccountId))
                .ToListAsync(ct);
            return Result.Success<IReadOnlyList<ConfigValueItem>>(rows);
        }
    }
}
