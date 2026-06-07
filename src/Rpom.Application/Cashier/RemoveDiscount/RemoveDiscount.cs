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

namespace Rpom.Application.Cashier.RemoveDiscount;

/// <summary>
/// Remove the current discount policy from the ticket. Clears all discount percentages
/// and amounts, then recomputes the ticket. Idempotent — OK if no discount is applied.
/// Requires the table lock.
/// </summary>
public static class RemoveDiscount
{
    public sealed record Command(long TicketId) : ICommand<Response>;

    public sealed record Response(long TicketId, decimal TotalAmount);

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

            var now = clock.UtcNow;
            var ticketEntity = await db.Tickets.FirstAsync(t => t.Id == ticket.Id, ct);

            if (ticketEntity.DiscountPolicyId is null && ticketEntity.DiscountPercent == 0m)
            {
                // Idempotent: no discount applied, return current total.
                return Result.Success(new Response(ticket.Id, ticketEntity.TotalAmount));
            }

            ticketEntity.DiscountPercent = 0m;
            ticketEntity.DiscountPolicyId = null;
            ticketEntity.UpdatedAt = now;

            var orderItems = await db.OrderItems
                .Where(o => o.TicketId == ticket.Id && o.Status != OrderItemStatus.Cancelled)
                .ToListAsync(ct);

            foreach (var o in orderItems)
            {
                o.LineDiscountPercent = 0m;
                o.LineDiscountAmount = 0m;
                o.TicketDiscountPercent = 0m;
                o.TicketDiscountAmount = 0m;
                o.UpdatedAt = now;
            }

            await ticketRecompute.RecomputeAsync(ticket.Id, ct);

            var staff = await db.StaffAccounts.FirstAsync(s => s.Id == currentStaff.StaffAccountId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = ticket.Id,
                Action = "REMOVE_DISCOUNT",
                ActorStaffAccountId = currentStaff.StaffAccountId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Discount removed from ticket {ticket.Id}",
            });

            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"Discount.Remove(ticketId={ticket.Id})", ct);

            var total = await db.Tickets.Where(t => t.Id == ticket.Id)
                .Select(t => t.TotalAmount).FirstAsync(ct);

            return Result.Success(new Response(ticket.Id, total));
        }
    }
}
