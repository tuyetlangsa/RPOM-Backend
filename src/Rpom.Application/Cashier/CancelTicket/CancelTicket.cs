using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.CancelTicket;

/// <summary>
///     Cancel an entire OPEN ticket (e.g. opened by mistake / guest left). The bill must be
///     empty — every order item already CANCELLED and no recorded payment — and a manager must
///     authorize it. The unsent DRAFT cart is dropped, the table lock released, and the ticket
///     moves to the hard-terminal CANCELLED state. No payment money is ever touched here.
/// </summary>
public static class CancelTicket
{
    public sealed record Command(
        long TicketId, int ManagerStaffId, int CancellationReasonId, string? CancellationNote) : ICommand<Response>;

    public sealed record Response(long TicketId, string Status, DateTime CancelledAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);
            RuleFor(x => x.ManagerStaffId).GreaterThan(0);
            RuleFor(x => x.CancellationReasonId).GreaterThan(0);
            RuleFor(x => x.CancellationNote).MaximumLength(500);
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
            int staffId = currentStaff.StaffAccountId;

            Ticket? ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct);
            if (ticket is null) return Result.Failure<Response>(TicketErrors.NotFound);
            if (ticket.Status != TicketStatus.Open) return Result.Failure<Response>(TicketErrors.NotOpen);

            Result held = await guard.EnsureHeldAsync(ticket.TableId, staffId, ct);
            if (held.IsFailure) return Result.Failure<Response>(held.Error);

            // Authorizing manager must exist, be active/unlocked, and hold a manager-level role.
            var manager = await db.StaffAccounts
                .Where(s => s.Id == request.ManagerStaffId)
                .Select(s => new { s.FullName, s.IsActive, s.IsLocked, RoleCode = s.Role.Code })
                .FirstOrDefaultAsync(ct);
            if (manager is null || !manager.IsActive || manager.IsLocked ||
                (manager.RoleCode != Roles.Owner && manager.RoleCode != Roles.Manager))
            {
                return Result.Failure<Response>(TicketErrors.InvalidManager);
            }

            // Cancellation reason must exist and still be active.
            var reason = await db.CancellationReasons
                .Where(r => r.Id == request.CancellationReasonId)
                .Select(r => new { r.Name, r.IsActive })
                .FirstOrDefaultAsync(ct);
            if (reason is null || !reason.IsActive)
            {
                return Result.Failure<Response>(TicketErrors.InvalidCancellationReason);
            }

            // Money guards: a successful payment blocks outright; a pending one must be cleared first.
            if (await db.TicketPaymentDetails
                    .AnyAsync(p => p.TicketId == ticket.Id && p.Status == TicketPaymentStatus.Success, ct))
            {
                return Result.Failure<Response>(TicketErrors.HasSuccessfulPayment);
            }
            if (await db.TicketPaymentDetails
                    .AnyAsync(p => p.TicketId == ticket.Id && p.Status == TicketPaymentStatus.Pending, ct))
            {
                return Result.Failure<Response>(TicketErrors.HasPendingPayment);
            }

            // Bill must be empty: no order item left in a non-cancelled state (amount = 0).
            if (await db.OrderItems
                    .AnyAsync(oi => oi.TicketId == ticket.Id && oi.Status != OrderItemStatus.Cancelled, ct))
            {
                return Result.Failure<Response>(TicketErrors.HasActiveItems);
            }

            DateTime now = clock.UtcNow;

            // Drop the unsent DRAFT cart (kitchen never saw it) and close its draft orders.
            List<long> draftOrderIds = await db.Orders
                .Where(o => o.TicketId == ticket.Id && o.Status == OrderStatus.Draft)
                .Select(o => o.Id)
                .ToListAsync(ct);
            if (draftOrderIds.Count > 0)
            {
                List<CartItem> cartItems = await db.CartItems
                    .Where(c => draftOrderIds.Contains(c.OrderId))
                    .ToListAsync(ct);
                db.CartItems.RemoveRange(cartItems); // CartItemDetail cascades
                List<Order> draftOrders = await db.Orders
                    .Where(o => draftOrderIds.Contains(o.Id))
                    .ToListAsync(ct);
                foreach (Order o in draftOrders)
                {
                    o.Status = OrderStatus.Deleted;
                    o.UpdatedAt = now;
                }
            }

            ticket.Status = TicketStatus.Cancelled;
            ticket.CancelledAt = now;
            ticket.CancellationReasonId = request.CancellationReasonId;
            ticket.CancellationNote = request.CancellationNote;
            ticket.ManagerStaffId = request.ManagerStaffId;

            string? actorName = await db.StaffAccounts
                .Where(s => s.Id == staffId).Select(s => s.FullName).FirstOrDefaultAsync(ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = ticket.Id,
                Action = "CANCEL",
                ActorStaffAccountId = staffId,
                ActorFullName = actorName,
                Timestamp = now,
                Summary = $"Status: OPEN → CANCELLED, reason: {reason.Name}, approved by: {manager.FullName}"
            });

            // Free the table — drop the operation lock so other terminals can use it.
            TableLock? lockRow = await db.TableLocks
                .FirstOrDefaultAsync(l => l.TableId == ticket.TableId, ct);
            if (lockRow is not null)
            {
                db.TableLocks.Remove(lockRow);
            }

            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Ticket.Cancel(id={ticket.Id})", ct);

            return Result.Success(new Response(ticket.Id, ticket.Status, now));
        }
    }
}
