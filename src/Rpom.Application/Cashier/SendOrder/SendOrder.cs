using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.SendOrder;

/// <summary>
/// Send the ticket's DRAFT cart to the kitchen: copy CartItem→OrderItem (+ details), snapshot
/// each item's KitchenStationId at send time, flip the order DRAFT→SENT, clear the cart, then
/// recompute the ticket. Bumps FLOOR_PLAN + KITCHEN. Requires the table lock.
/// </summary>
public static class SendOrder
{
    public sealed record Command(long TicketId) : ICommand<Response>;

    public sealed record Response(long OrderId, short OrderNumber, int ItemCount, decimal TotalAmount);

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        ITableOperationGuard guard,
        ITicketRecomputeService ticketRecompute,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var ticket = await db.Tickets
                .Where(t => t.Id == request.TicketId)
                .Select(t => new { t.Id, t.TableId, t.Status })
                .FirstOrDefaultAsync(ct);
            if (ticket is null) return Result.Failure<Response>(TicketErrors.NotFound);
            if (ticket.Status != TicketStatus.Open) return Result.Failure<Response>(TicketErrors.NotOpen);

            var held = await guard.EnsureHeldAsync(ticket.TableId, currentStaff.StaffAccountId, ct);
            if (held.IsFailure) return Result.Failure<Response>(held.Error);

            var order = await db.Orders
                .FirstOrDefaultAsync(o => o.TicketId == ticket.Id && o.Status == OrderStatus.Draft, ct);
            if (order is null) return Result.Failure<Response>(OrderErrors.EmptyCart);

            var cartItems = await db.CartItems.Where(c => c.OrderId == order.Id).ToListAsync(ct);
            if (cartItems.Count == 0) return Result.Failure<Response>(OrderErrors.EmptyCart);

            var cartIds = cartItems.Select(c => c.Id).ToList();
            var cartDetails = await db.CartItemDetails
                .Where(d => cartIds.Contains(d.CartItemId))
                .ToListAsync(ct);
            var detailsByCart = cartDetails.GroupBy(d => d.CartItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Snapshot each item's kitchen station at send time.
            var itemIds = cartItems.Select(c => c.ItemId).Distinct().ToList();
            var kitchenByItem = await db.Items
                .Where(i => itemIds.Contains(i.Id))
                .Select(i => new { i.Id, i.KitchenStationId })
                .ToListAsync(ct);
            var stationByItem = kitchenByItem.ToDictionary(x => x.Id, x => x.KitchenStationId);

            var now = clock.UtcNow;

            foreach (var c in cartItems)
            {
                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    TicketId = ticket.Id,
                    ItemId = c.ItemId,
                    ItemCode = c.ItemCode,
                    ItemName = c.ItemName,
                    UomId = c.UomId,
                    UomCode = c.UomCode,
                    UomName = c.UomName,
                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice,
                    ChoicePricePerUnit = c.ChoicePricePerUnit,
                    VatPercent = c.VatPercent,
                    ServiceChargePercent = c.ServiceChargePercent,
                    ServiceChargeVatPercent = c.ServiceChargeVatPercent,
                    KitchenStationId = stationByItem.GetValueOrDefault(c.ItemId),
                    Status = OrderItemStatus.Pending,
                    SentAt = now,
                    Notes = c.Notes,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.OrderItems.Add(orderItem);
                await db.SaveChangesAsync(ct); // need orderItem.Id for its details

                foreach (var d in detailsByCart.GetValueOrDefault(c.Id) ?? [])
                {
                    db.OrderItemDetails.Add(new OrderItemDetail
                    {
                        OrderItemId = orderItem.Id,
                        ChoiceCategoryId = d.ChoiceCategoryId,
                        ItemId = d.ItemId,
                        ItemName = d.ItemName,
                        ComponentType = d.ComponentType,
                        Quantity = d.Quantity,
                        ExtraPrice = d.ExtraPrice,
                        Notes = d.Notes,
                        CreatedAt = now,
                    });
                }
            }

            // Clear the cart and flip the order to SENT.
            db.CartItems.RemoveRange(cartItems);
            order.Status = OrderStatus.Sent;
            order.SentAt = now;
            order.UpdatedAt = now;

            var staff = await db.StaffAccounts.FirstAsync(s => s.Id == currentStaff.StaffAccountId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Order),
                EntityId = order.Id,
                Action = "SEND_KITCHEN",
                ActorStaffAccountId = currentStaff.StaffAccountId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Order #{order.OrderNumber} sent: {cartItems.Count} items (ticket {ticket.Id})",
            });
            await db.SaveChangesAsync(ct);

            await ticketRecompute.RecomputeAsync(ticket.Id, ct);
            await db.SaveChangesAsync(ct);

            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Order.Send(ticketId={ticket.Id})", ct);
            await versionService.BumpAsync(VersionScopes.Kitchen, $"Order.Send(ticketId={ticket.Id})", ct);

            var totalAmount = await db.Tickets.Where(t => t.Id == ticket.Id)
                .Select(t => t.TotalAmount).FirstAsync(ct);

            return Result.Success(new Response(order.Id, order.OrderNumber, cartItems.Count, totalAmount));
        }
    }
}
