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
/// Apply a discount policy to an open ticket. Evaluates conditions against the current
/// ticket state, picks the best-matching condition, then applies PERCENT or distributes FIXED.
/// Requires the table lock; recomputes the ticket.
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
        IRoundingConfig rc,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var ticket = await db.Tickets
                .Where(t => t.Id == request.TicketId)
                .Select(t => new { t.Id, t.TableId, t.AreaId, t.Status, t.Subtotal })
                .FirstOrDefaultAsync(ct);
            if (ticket is null) return Result.Failure<Response>(TicketErrors.NotFound);
            if (ticket.Status != TicketStatus.Open) return Result.Failure<Response>(TicketErrors.NotOpen);

            var held = await guard.EnsureHeldAsync(ticket.TableId, currentStaff.StaffAccountId, ct);
            if (held.IsFailure) return Result.Failure<Response>(held.Error);

            // One policy at a time.
            var currentTicket = await db.Tickets.FirstAsync(t => t.Id == ticket.Id, ct);
            if (currentTicket.DiscountPolicyId is not null)
                return Result.Failure<Response>(DiscountErrors.AlreadyApplied);

            var policy = await db.DiscountPolicies
                .Where(p => p.Id == request.DiscountPolicyId && p.IsActive)
                .Include(p => p.Conditions.OrderBy(c => c.DisplayOrder))
                .FirstOrDefaultAsync(ct);
            if (policy is null) return Result.Failure<Response>(DiscountErrors.PolicyNotFound);

            // DaysOfWeek gate.
            var today = ((int)clock.UtcNow.DayOfWeek + 6) % 7 + 1; // Mon=1..Sun=7
            if (!string.IsNullOrEmpty(policy.DaysOfWeek))
            {
                var allowed = policy.DaysOfWeek.Split(',').Select(d => int.Parse(d.Trim())).ToHashSet();
                if (!allowed.Contains(today))
                    return Result.Failure<Response>(DiscountErrors.DaysOfWeekMismatch);
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
                return Result.Failure<Response>(DiscountErrors.NotApplicable);

            // ---- Apply ----
            var now = clock.UtcNow;
            currentTicket.DiscountPolicyId = policy.Id;

            if (evalResult.ApplyType == DiscountApplyType.Percent)
            {
                ApplyPercent(policy.DiscountType, currentTicket, orderItems, evalResult.DiscountValue,
                    evalResult.MatchedItemId);
            }
            else
            {
                DistributeFixed(policy.DiscountType, orderItems, evalResult, rc);
            }

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

        private static void ApplyPercent(
            string discountType, Ticket ticket, IReadOnlyList<OrderItem> orderItems,
            decimal percent, int? matchedItemId)
        {
            if (discountType == DiscountType.TicketThreshold)
            {
                ticket.DiscountPercent = percent;
            }
            else
            {
                ticket.DiscountPercent = 0m;
                foreach (var o in orderItems.Where(o => o.ItemId == matchedItemId))
                {
                    o.LineDiscountPercent = percent;
                    o.TicketDiscountPercent = 0m;
                }
            }
        }

        private static void DistributeFixed(
            string discountType, IReadOnlyList<OrderItem> orderItems,
            DiscountEvaluator.Result evalResult, IRoundingConfig rc)
        {
            var affected = discountType == DiscountType.TicketThreshold
                ? orderItems
                : orderItems.Where(o => o.ItemId == evalResult.MatchedItemId).ToList();

            if (affected.Count == 0) return;

            decimal totalSubtotal = affected.Sum(o => o.LineSubtotal);
            if (totalSubtotal <= 0m) return;

            decimal remaining = evalResult.DiscountValue;
            var ordered = affected.OrderBy(o => o.SentAt).ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                var o = ordered[i];
                if (i == ordered.Count - 1)
                {
                    // Last line absorbs rounding error — ensures exact sum.
                    if (discountType == DiscountType.TicketThreshold)
                        o.TicketDiscountAmount = remaining;
                    else
                        o.LineDiscountAmount = remaining;
                }
                else
                {
                    decimal share = Money.Round(
                        evalResult.DiscountValue * o.LineSubtotal / totalSubtotal,
                        rc, RoundingKeys.LineDiscount);
                    if (discountType == DiscountType.TicketThreshold)
                        o.TicketDiscountAmount = share;
                    else
                        o.LineDiscountAmount = share;
                    remaining -= share;
                }

                o.LineDiscountPercent = 0m;
                o.TicketDiscountPercent = 0m;
            }
        }
    }
}
