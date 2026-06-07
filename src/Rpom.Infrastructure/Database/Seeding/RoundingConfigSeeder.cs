using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Domain.Configuration;

namespace Rpom.Infrastructure.Database.Seeding;

/// <summary>
///     Idempotent seeder for the 14 RoundingConfig keys (pricing spec §1).
///     Inserts only keys missing from DB; never overwrites Owner-edited digits.
/// </summary>
public sealed class RoundingConfigSeeder(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RoundingConfigSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var existing = (await db.Set<RoundingConfig>()
            .Select(x => x.KeyCode)
            .ToListAsync(ct)).ToHashSet();

        var toAdd = RoundingKeys.Defaults
            .Where(kv => !existing.Contains(kv.Key))
            .Select(kv => new RoundingConfig
            {
                KeyCode = kv.Key,
                Digits = kv.Value,
                Description = $"Rounding digits for {kv.Key}"
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            db.Set<RoundingConfig>().AddRange(toAdd);
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("RoundingConfigSeeder finished — {Added} new rows seeded ({Total} keys in catalog).",
            toAdd.Count, RoundingKeys.Defaults.Count);
    }
}
