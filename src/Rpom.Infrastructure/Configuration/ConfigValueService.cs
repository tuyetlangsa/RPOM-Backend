using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Domain.Configuration;

namespace Rpom.Infrastructure.Configuration;

internal sealed class ConfigValueService(IDbContext dbContext, IDateTimeProvider clock)
    : IConfigValueService
{
    public async Task<string?> GetAsync(string code, CancellationToken ct = default)
    {
        return await dbContext.ConfigValues
            .Where(x => x.Code == code)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> ExistsAsync(string code, CancellationToken ct = default) =>
        dbContext.ConfigValues.AnyAsync(x => x.Code == code, ct);

    public async Task<bool> SetAsync(string code, string? value, int? updatedByStaffAccountId,
        CancellationToken ct = default)
    {
        ConfigValue? row = await dbContext.ConfigValues.FirstOrDefaultAsync(x => x.Code == code, ct);
        if (row is null)
        {
            return false;
        }

        row.Value = value;
        row.UpdatedAt = clock.UtcNow;
        row.UpdatedByStaffAccountId = updatedByStaffAccountId;
        await dbContext.SaveChangesAsync(ct);
        return true;
    }
}
