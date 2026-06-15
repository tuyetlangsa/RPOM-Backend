using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier;

internal static class OrderRollup
{
    /// <summary>
    ///     For each given order, roll it up to DONE when every one of its own order items is
    ///     terminal (DONE or CANCELLED). The status check reads via projection (server-side SQL),
    ///     so callers must SaveChanges any pending item-status changes BEFORE calling this.
    ///     Caller SaveChanges again afterwards to persist the order bump.
    /// </summary>
    public static async Task BumpFullyTerminalOrdersAsync(
        IDbContext db, IReadOnlyList<long> orderIds, DateTime now, CancellationToken ct)
    {
        if (orderIds.Count == 0) return;

        var itemStatuses = await db.OrderItems
            .Where(oi => orderIds.Contains(oi.OrderId))
            .Select(oi => new { oi.OrderId, oi.Status })
            .ToListAsync(ct);

        var doneOrderIds = orderIds
            .Where(oid => itemStatuses.Where(s => s.OrderId == oid)
                .All(s => s.Status == OrderItemStatus.Done || s.Status == OrderItemStatus.Cancelled))
            .ToList();
        if (doneOrderIds.Count == 0) return;

        var orders = await db.Orders.Where(o => doneOrderIds.Contains(o.Id)).ToListAsync(ct);
        foreach (var o in orders.Where(o => o.Status != OrderStatus.Done && o.Status != OrderStatus.Deleted))
        {
            o.Status = OrderStatus.Done;
            o.UpdatedAt = now;
        }
    }
}
