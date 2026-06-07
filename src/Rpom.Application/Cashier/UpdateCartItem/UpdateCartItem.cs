using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.UpdateCartItem;

/// <summary>
/// Change a cart line's quantity and notes (reconfiguring modifiers = remove + add).
/// Requires the table lock; recomputes the cart.
/// </summary>
public static class UpdateCartItem
{
    public sealed record Command(long TicketId, long CartItemId, decimal Quantity, string? Notes)
        : ICommand<Response>;

    public sealed record Response(long CartItemId, decimal LineTotal);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Quantity).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        ITableOperationGuard guard,
        ICartRecomputeService cartRecompute,
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

            // Cart item must belong to this ticket's DRAFT order.
            var cartItem = await db.CartItems
                .Where(c => c.Id == request.CartItemId
                    && db.Orders.Any(o => o.Id == c.OrderId
                        && o.TicketId == ticket.Id && o.Status == OrderStatus.Draft))
                .FirstOrDefaultAsync(ct);
            if (cartItem is null) return Result.Failure<Response>(OrderErrors.CartItemNotFound);

            var now = clock.UtcNow;
            cartItem.Quantity = request.Quantity;
            cartItem.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            cartItem.UpdatedAt = now;

            await cartRecompute.RecomputeAsync(cartItem.OrderId, ct);
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Cart.Update(ticketId={ticket.Id})", ct);

            return Result.Success(new Response(cartItem.Id, cartItem.LineTotal));
        }
    }
}
