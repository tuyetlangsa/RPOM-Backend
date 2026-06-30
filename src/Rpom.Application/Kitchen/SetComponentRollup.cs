using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Domain.Sales;

namespace Rpom.Application.Kitchen;

/// <summary>
///     Derives a SET-MENU parent <see cref="OrderItem"/>'s status from its kitchen-routed
///     components (OrderItemDetail). Parent → PROCESSING when any component started, READY when all
///     components READY/DONE, DONE when all DONE. Reads component status via projection (server-side
///     SQL) → caller must SaveChanges component changes BEFORE calling; caller SaveChanges again after.
/// </summary>
internal static class SetComponentRollup
{
    public static async Task RecomputeAsync(
        IDbContext db, IReadOnlyList<long> parentOrderItemIds, DateTime now, CancellationToken ct)
    {
        if (parentOrderItemIds.Count == 0) return;

        var comps = await db.OrderItemDetails
            .Where(d => parentOrderItemIds.Contains(d.OrderItemId) && d.KitchenStationId != null)
            .Select(d => new { d.OrderItemId, d.Status })
            .ToListAsync(ct);
        if (comps.Count == 0) return;

        var byParent = comps.GroupBy(c => c.OrderItemId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Status).ToList());
        var parentIds = byParent.Keys.ToList();

        var parents = await db.OrderItems.Where(o => parentIds.Contains(o.Id)).ToListAsync(ct);
        foreach (var p in parents)
        {
            if (!byParent.TryGetValue(p.Id, out var statuses)) continue;

            string target =
                statuses.All(s => s == OrderItemStatus.Done) ? OrderItemStatus.Done
                : statuses.All(s => s is OrderItemStatus.Done or OrderItemStatus.Ready) ? OrderItemStatus.Ready
                : statuses.Any(s => s is OrderItemStatus.Processing or OrderItemStatus.Ready or OrderItemStatus.Done)
                    ? OrderItemStatus.Processing
                : OrderItemStatus.Pending;

            if (p.Status == target) continue;
            p.Status = target;
            p.UpdatedAt = now;
            if (target == OrderItemStatus.Processing && p.StartCookAt is null) p.StartCookAt = now;
            if (target == OrderItemStatus.Ready && p.ReadyAt is null) p.ReadyAt = now;
            if (target == OrderItemStatus.Done && p.DoneAt is null) p.DoneAt = now;
        }
    }
}
