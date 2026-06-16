using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Inventory;
using Rpom.Domain.Inventory;
using Rpom.Domain.Menu;

namespace Rpom.Infrastructure.Inventory;

internal sealed class StockMovementService(
    IDbContext dbContext,
    IDateTimeProvider clock) : IStockMovementService
{
    public async Task<StockMovement> CreateManualAsync(
        int itemId, string movementType, decimal signedQty,
        string? reason, int staffId, DateTime now, CancellationToken ct)
    {
        // Defensive: caller handler already validated, but guard against misuse.
        var item = await dbContext.Items
            .Where(x => x.Id == itemId)
            .Select(x => new { x.IsStockable })
            .FirstOrDefaultAsync(ct);
        if (item is null || !item.IsStockable)
            throw new InvalidOperationException($"Item {itemId} is not stockable or not found.");

        decimal lastBalance = await GetLastBalanceAsync(itemId, ct);
        decimal newBalance = lastBalance + signedQty;

        var movement = new StockMovement
        {
            ItemId = itemId,
            MovementType = movementType,
            QtyInBase = signedQty,
            BalanceAfter = newBalance,
            ReferenceType = StockMovementReferenceType.Manual,
            Reason = reason,
            CreatedByStaffId = staffId,
            CreatedAt = now
        };
        dbContext.StockMovements.Add(movement);

        await UpsertItemStockAsync(itemId, newBalance, now, ct);
        await dbContext.SaveChangesAsync(ct);

        return movement;
    }

    public async Task DeductAsync(long orderItemId, int staffId, CancellationToken ct)
    {
        var orderItem = await dbContext.OrderItems
            .Include(oi => oi.Item)
            .FirstOrDefaultAsync(oi => oi.Id == orderItemId, ct);
        if (orderItem is null) return;
        if (orderItem.Quantity <= 0) return; // refund / zero-qty lines

        var item = orderItem.Item;
        // A recipe dish is itself NOT stockable — it consumes stockable materials via BOM.
        // Only skip when the item neither has a recipe nor is a directly-stockable item.
        if (item is null || (!item.IsStockable && !item.HasRecipe)) return;

        DateTime now = clock.UtcNow;

        if (item.HasRecipe)
        {
            // Assumption: BomLine.Quantity is in MaterialItem.BaseUomId already.
            // No ItemUomConversion needed for v1.
            var bomLines = await dbContext.BomLines
                .Where(bl => bl.SellableItemId == item.Id && bl.IsActive)
                .ToListAsync(ct);

            foreach (var bl in bomLines)
            {
                decimal signedQty = -(bl.Quantity * orderItem.Quantity);
                decimal lastBalance = await GetLastBalanceAsync(bl.MaterialItemId, ct);
                decimal newBalance = lastBalance + signedQty;

                dbContext.StockMovements.Add(new StockMovement
                {
                    ItemId = bl.MaterialItemId,
                    MovementType = StockMovementType.Deduct,
                    QtyInBase = signedQty,
                    BalanceAfter = newBalance,
                    ReferenceType = StockMovementReferenceType.OrderDish,
                    ReferenceId = orderItemId,
                    Reason = $"BOM: {item.Code} x{orderItem.Quantity}",
                    CreatedByStaffId = staffId,
                    CreatedAt = now
                });

                await UpsertItemStockAsync(bl.MaterialItemId, newBalance, now, ct);
            }
        }
        else
        {
            // Non-recipe stockable item: deduct the item itself.
            decimal signedQty = -orderItem.Quantity;
            decimal lastBalance = await GetLastBalanceAsync(item.Id, ct);
            decimal newBalance = lastBalance + signedQty;

            dbContext.StockMovements.Add(new StockMovement
            {
                ItemId = item.Id,
                MovementType = StockMovementType.Deduct,
                QtyInBase = signedQty,
                BalanceAfter = newBalance,
                ReferenceType = StockMovementReferenceType.OrderDish,
                ReferenceId = orderItemId,
                CreatedByStaffId = staffId,
                CreatedAt = now
            });

            await UpsertItemStockAsync(item.Id, newBalance, now, ct);
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task<decimal> GetLastBalanceAsync(int itemId, CancellationToken ct)
    {
        var last = await dbContext.StockMovements
            .Where(sm => sm.ItemId == itemId)
            .OrderByDescending(sm => sm.Id)
            .Select(sm => (decimal?)sm.BalanceAfter)
            .FirstOrDefaultAsync(ct);
        return last ?? 0m;
    }

    private async Task UpsertItemStockAsync(int itemId, decimal currentQty, DateTime now, CancellationToken ct)
    {
        var stock = await dbContext.ItemStocks
            .FirstOrDefaultAsync(s => s.ItemId == itemId, ct);

        if (stock is null)
        {
            stock = new ItemStock { ItemId = itemId };
            dbContext.ItemStocks.Add(stock);
        }

        stock.CurrentQty = currentQty;
        stock.LastMovementAt = now;
        stock.UpdatedAt = now;
    }
}
