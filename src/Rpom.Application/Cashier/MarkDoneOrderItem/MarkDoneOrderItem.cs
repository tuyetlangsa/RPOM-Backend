using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.MarkDoneOrderItem;

/// <summary>
///     Order staff marks one or more items as DONE (served to customer). READY → DONE.
///     If all order items across all batches for this ticket are terminal (DONE or CANCELLED),
///     bump the parent orders to DONE as well.
/// </summary>
public static class MarkDoneOrderItem
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

            // Set menu có thành phần bếp → phải done theo từng component, không done cả set trực tiếp.
            bool anySetParent = await db.OrderItemDetails
                .AnyAsync(d => ids.Contains(d.OrderItemId) && d.KitchenStationId != null, ct);
            if (anySetParent) return Result.Failure<Response>(OrderItemErrors.SetUseComponent);

            if (items.Any(oi => oi.Status != OrderItemStatus.Ready))
                return Result.Failure<Response>(OrderItemErrors.NotReady);

            DateTime now = clock.UtcNow;
            foreach (var oi in items)
            {
                oi.Status = OrderItemStatus.Done;
                oi.DoneAt = now;
                oi.UpdatedAt = now;
            }

            // Persist the DONE transitions FIRST: the rollup reads item status via projection
            // (server-side SQL), which does not see unsaved tracked changes.
            await db.SaveChangesAsync(ct);

            // Per affected order: if all its own items are terminal, roll the order up to DONE.
            await OrderRollup.BumpFullyTerminalOrdersAsync(
                db, items.Select(oi => oi.OrderId).Distinct().ToList(), now, ct);

            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Kitchen, $"MarkDone(ticketId={ticket.Id})", ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"MarkDone(ticketId={ticket.Id})", ct);

            return Result.Success(new Response(items.Count, OrderItemStatus.Done));
        }
    }
}
