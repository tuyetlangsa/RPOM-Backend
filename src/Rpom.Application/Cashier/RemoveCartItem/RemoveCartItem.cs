using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.RemoveCartItem;

/// <summary>
/// Remove a cart line (its detail rows cascade) and recompute the cart. Requires the table lock.
/// </summary>
public static class RemoveCartItem
{
    public sealed record Command(long TicketId, long CartItemId) : ICommand;

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        ITableOperationGuard guard,
        ICartRecomputeService cartRecompute,
        IVersionService versionService) : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var ticket = await db.Tickets
                .Where(t => t.Id == request.TicketId)
                .Select(t => new { t.Id, t.TableId, t.Status })
                .FirstOrDefaultAsync(ct);
            if (ticket is null) return Result.Failure(TicketErrors.NotFound);
            if (ticket.Status != TicketStatus.Open) return Result.Failure(TicketErrors.NotOpen);

            var held = await guard.EnsureHeldAsync(ticket.TableId, currentStaff.StaffAccountId, ct);
            if (held.IsFailure) return Result.Failure(held.Error);

            var cartItem = await db.CartItems
                .Where(c => c.Id == request.CartItemId
                    && db.Orders.Any(o => o.Id == c.OrderId
                        && o.TicketId == ticket.Id && o.Status == OrderStatus.Draft))
                .FirstOrDefaultAsync(ct);
            if (cartItem is null) return Result.Failure(OrderErrors.CartItemNotFound);

            var orderId = cartItem.OrderId;
            db.CartItems.Remove(cartItem);
            await db.SaveChangesAsync(ct);

            await cartRecompute.RecomputeAsync(orderId, ct);
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Cart.Remove(ticketId={ticket.Id})", ct);

            return Result.Success();
        }
    }
}
