using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Rpom.Infrastructure.Database;

/// <summary>
/// Auto-increments the <c>Version</c> property on every tracked entity that has one
/// whenever its state is <see cref="EntityState.Modified"/>. Together with
/// <see cref="Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder.IsConcurrencyToken"/>
/// this turns <c>Version</c> into a true optimistic-concurrency check: the UPDATE
/// WHERE clause includes the old Version, and we bump it so the next concurrent
/// writer will get a <see cref="DbUpdateConcurrencyException"/>.
/// </summary>
internal sealed class ConcurrencyVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        BumpVersions(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        BumpVersions(eventData.Context);
        return new ValueTask<InterceptionResult<int>>(result);
    }

    private static void BumpVersions(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified) continue;

            var versionProp = entry.Metadata.FindProperty("Version");
            if (versionProp is null || versionProp.ClrType != typeof(int)) continue;

            var current = (int)entry.Property("Version").CurrentValue!;
            entry.Property("Version").CurrentValue = current + 1;
        }
    }
}
