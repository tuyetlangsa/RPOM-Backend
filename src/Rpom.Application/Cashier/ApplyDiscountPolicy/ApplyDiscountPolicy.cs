using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.DiscountPolicies;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.ApplyDiscountPolicy;

/// <summary>
/// Apply a discount policy to an open ticket. Validates conditions against the current
/// ticket state, attaches the policy ID, then delegates all discount math to
/// TicketRecomputeService (which converts FIXED→% and distributes via DiscountResolver).
/// Requires the table lock.
/// </summary>
public static class ApplyDiscountPolicy
{
    public sealed record Command(long TicketId, int DiscountPolicyId) : ICommand<Response>;

    public sealed record Response(long TicketId, decimal DiscountAmount, decimal TotalAmount);

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
                .Select(t => new { t.Id, t.TableId, t.AreaId, t.Status, t.Subtotal })
                .FirstOrDefaultAsync(ct);
            if (ticket is null)
            {
                return Result.Failure<Response>(TicketErrors.NotFound);
            }

            if (ticket.Status != TicketStatus.Open)
            {
                return Result.Failure<Response>(TicketErrors.NotOpen);
            }

            var held = await guard.EnsureHeldAsync(ticket.TableId, currentStaff.StaffAccountId, ct);
            if (held.IsFailure)
            {
                return Result.Failure<Response>(held.Error);
            }

            // One policy at a time.
            var currentTicket = await db.Tickets.FirstAsync(t => t.Id == ticket.Id, ct);
            if (currentTicket.DiscountPolicyId is not null)
            {
                return Result.Failure<Response>(DiscountErrors.AlreadyApplied);
            }

            var policy = await db.DiscountPolicies
                .Where(p => p.Id == request.DiscountPolicyId && p.IsActive)
                .Include(p => p.Conditions.OrderBy(c => c.DisplayOrder))
                .FirstOrDefaultAsync(ct);
            if (policy is null)
            {
                return Result.Failure<Response>(DiscountErrors.PolicyNotFound);
            }

            // DaysOfWeek gate.
            var today = ((int)clock.UtcNow.DayOfWeek + 6) % 7 + 1; // Mon=1..Sun=7
            if (!string.IsNullOrEmpty(policy.DaysOfWeek))
            {
                var allowed = policy.DaysOfWeek.Split(',').Select(d => int.Parse(d.Trim())).ToHashSet();
                if (!allowed.Contains(today))
                {
                    return Result.Failure<Response>(DiscountErrors.DaysOfWeekMismatch);
                }
            }

            // Build item buckets from non-cancelled order items.
            var orderItems = await db.OrderItems
                .Where(o => o.TicketId == ticket.Id && o.Status != OrderItemStatus.Cancelled)
                .ToListAsync(ct);

            var buckets = orderItems
                .GroupBy(o => o.ItemId)
                .Select(g => new DiscountEvaluator.ItemBucket(
                    g.Key, g.Sum(o => o.Quantity), g.Sum(o => o.LineSubtotal)))
                .ToList();

            var conditionSpecs = policy.Conditions.Select(c => new DiscountEvaluator.ConditionSpec(
                c.ThresholdAmount, c.ItemId, c.QuantityThreshold,
                c.AreaId, c.ApplyType, c.DiscountValue)).ToList();

            var evalResult = DiscountEvaluator.Evaluate(
                policy.DiscountType, policy.DaysOfWeek, today,
                ticket.Subtotal, ticket.AreaId, buckets, conditionSpecs);

            if (evalResult is null)
            {
                return Result.Failure<Response>(DiscountErrors.NotApplicable);
            }

            // ---- Attach policy; TicketRecomputeService derives all discount math. ----
            var now = clock.UtcNow;
            currentTicket.DiscountPolicyId = policy.Id;
            // Percents are derived by TicketRecomputeService from the attached policy.

            await ticketRecompute.RecomputeAsync(ticket.Id, ct);

            var staff = await db.StaffAccounts.FirstAsync(s => s.Id == currentStaff.StaffAccountId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = ticket.Id,
                Action = "APPLY_DISCOUNT",
                ActorStaffAccountId = currentStaff.StaffAccountId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Discount policy \"{policy.Code}\" applied: {evalResult.ApplyType} {evalResult.DiscountValue}",
            });

            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"Discount.Apply(ticketId={ticket.Id})", ct);

            var final = await db.Tickets.Where(t => t.Id == ticket.Id)
                .Select(t => new { t.DiscountAmount, t.TotalAmount }).FirstAsync(ct);

            return Result.Success(new Response(ticket.Id, final.DiscountAmount, final.TotalAmount));
        }
    }
}
