using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Infrastructure.Database;

namespace Rpom.Infrastructure.Versioning;

/// <summary>
///     Postgres-backed implementation using <c>INSERT … ON CONFLICT DO UPDATE</c>
///     for race-free atomic increment.
/// </summary>
internal sealed class VersionService(
    ApplicationDbContext dbContext,
    IDateTimeProvider clock) : IVersionService
{
    public Task BumpAsync(string scope, string source, CancellationToken ct)
    {
        DateTime now = clock.UtcNow;
        // Postgres UPSERT — atomic per-row; concurrent bumps both succeed.
        return dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO domain_versions (scope, version, updated_at, updated_by_source)
            VALUES ({scope}, 1, {now}, {source})
            ON CONFLICT (scope) DO UPDATE
              SET version = domain_versions.version + 1,
                  updated_at = {now},
                  updated_by_source = {source}
        ", ct);
    }

    public async Task<IReadOnlyDictionary<string, long>> GetAsync(
        IReadOnlyList<string> scopes,
        CancellationToken ct)
    {
        if (scopes.Count == 0)
        {
            return new Dictionary<string, long>();
        }

        var rows = await dbContext.DomainVersions
            .Where(x => scopes.Contains(x.Scope))
            .Select(x => new { x.Scope, x.Version })
            .ToListAsync(ct);
        var map = scopes.Distinct().ToDictionary(s => s, _ => 0L);
        foreach (var row in rows)
        {
            map[row.Scope] = row.Version;
        }

        return map;
    }
}
