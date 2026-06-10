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

namespace Rpom.Application.Cashier.CancelOrderItem;

/// <summary>
///     Order staff cancels one or more pending order items. PENDING → CANCELLED.
///     Only cancellable from PENDING — once kitchen has started, use Refund instead.
///     If all items are terminal, parent orders → DONE.
/// </summary>
public static class CancelOrderItem
{
    public sealed record Command(long TicketId, IReadOnlyList<long> OrderItemIds) : ICommand<Response>;
    public sealed record Response(int UpdatedCount, string NewStatus);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);
            RuleFor(x => x.OrderItemIds).NotEmpty();
        }
    }

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

            Result held = await guard.EnsureHeldAsync(ticket.TableId, currentStaff.StaffAccountId, ct);
            if (held.IsFailure) return Result.Failure<Response>(held.Error);

            var ids = request.OrderItemIds.Distinct().ToList();
            var items = await db.OrderItems
                .Where(oi => ids.Contains(oi.Id) && oi.TicketId == request.TicketId)
                .ToListAsync(ct);

            if (items.Count != ids.Count) return Result.Failure<Response>(OrderItemErrors.WrongTicket);
            if (items.Any(oi => oi.Status != OrderItemStatus.Pending))
                return Result.Failure<Response>(OrderItemErrors.NotPending);

            DateTime now = clock.UtcNow;
            foreach (var oi in items)
            {
                oi.Status = OrderItemStatus.Cancelled;
                oi.UpdatedAt = now;
            }

            // If ALL items are terminal, close parent orders.
            var allItems = await db.OrderItems
                .Where(oi => oi.TicketId == request.TicketId)
                .Select(oi => new { oi.Id, oi.OrderId, oi.Status })
                .ToListAsync(ct);
            var allTerminal = allItems.All(oi =>
                oi.Status == OrderItemStatus.Done || oi.Status == OrderItemStatus.Cancelled);
            if (allTerminal)
            {
                var orderIds = allItems.Select(oi => oi.OrderId).Distinct().ToList();
                var orders = await db.Orders.Where(o => orderIds.Contains(o.Id)).ToListAsync(ct);
                foreach (var o in orders.Where(o => o.Status != OrderStatus.Done && o.Status != OrderStatus.Deleted))
                {
                    o.Status = OrderStatus.Done;
                    o.UpdatedAt = now;
                }
            }

            await db.SaveChangesAsync(ct);
            await ticketRecompute.RecomputeAsync(ticket.Id, ct);
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Kitchen, $"CancelItem(ticketId={ticket.Id})", ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"CancelItem(ticketId={ticket.Id})", ct);

            return Result.Success(new Response(items.Count, OrderItemStatus.Cancelled));
        }
    }
}
