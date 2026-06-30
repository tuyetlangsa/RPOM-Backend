using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.DiscountPolicies;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;
using Rpom.Domain.Operations;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.SendOrder;

/// <summary>
///     Send the ticket's DRAFT cart to the kitchen: copy CartItem→OrderItem (+ details), snapshot
///     each item's KitchenStationId at send time, flip the order DRAFT→SENT, clear the cart, then
///     recompute the ticket. After recompute, auto-evaluates IsAutoApply discount policies and
///     applies the best match (if any). Bumps FLOOR_PLAN + KITCHEN (+ PRICING if discount applied).
///     Requires the table lock.
/// </summary>
public static class SendOrder
{
    /// <param name="CartItemIds">
    ///     Cart lines to send. Null/empty = send the whole cart. When a strict subset is given,
    ///     the kept lines are moved to a new DRAFT order (next batch) and only the selected lines
    ///     are sent in the current order.
    /// </param>
    public sealed record Command(long TicketId, IReadOnlyList<long>? CartItemIds) : ICommand<Response>;

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
                .Select(t => new { t.Id, t.TableId, t.AreaId, t.Status })
                .FirstOrDefaultAsync(ct);
            if (ticket is null)
            {
                return Result.Failure<Response>(TicketErrors.NotFound);
            }

            if (ticket.Status != TicketStatus.Open)
            {
                return Result.Failure<Response>(TicketErrors.NotOpen);
            }

            Result held = await guard.EnsureHeldAsync(ticket.TableId, currentStaff.StaffAccountId, ct);
            if (held.IsFailure)
            {
                return Result.Failure<Response>(held.Error);
            }

            Order? order = await db.Orders
                .FirstOrDefaultAsync(o => o.TicketId == ticket.Id && o.Status == OrderStatus.Draft, ct);
            if (order is null)
            {
                return Result.Failure<Response>(OrderErrors.EmptyCart);
            }

            List<CartItem> cartItems = await db.CartItems.Where(c => c.OrderId == order.Id).ToListAsync(ct);
            if (cartItems.Count == 0)
            {
                return Result.Failure<Response>(OrderErrors.EmptyCart);
            }

            // Partition the cart into lines sent now vs kept for a later batch.
            List<CartItem> selected, kept;
            if (request.CartItemIds is { Count: > 0 } ids)
            {
                var idSet = ids.ToHashSet();
                var known = cartItems.Select(c => c.Id).ToHashSet();
                if (!idSet.All(known.Contains))
                {
                    return Result.Failure<Response>(OrderErrors.CartItemNotFound);
                }

                selected = cartItems.Where(c => idSet.Contains(c.Id)).ToList();
                kept = cartItems.Where(c => !idSet.Contains(c.Id)).ToList();
            }
            else
            {
                selected = cartItems;
                kept = [];
            }

            if (selected.Count == 0)
            {
                return Result.Failure<Response>(OrderErrors.EmptyCart);
            }

            // Chặn gửi bếp món đang bị khoá out-of-stock tại area của phiếu — kể cả món đã
            // được thêm vào cart TRƯỚC khi khoá. Dòng refund (Quantity < 0) là trả hàng, không chặn.
            var lockCandidateItemIds = selected
                .Where(c => c.Quantity > 0).Select(c => c.ItemId).Distinct().ToList();
            if (lockCandidateItemIds.Count > 0)
            {
                bool anyLocked = await db.ItemAreaLocks.AnyAsync(
                    l => l.AreaId == ticket.AreaId && lockCandidateItemIds.Contains(l.ItemId), ct);
                if (anyLocked)
                {
                    return Result.Failure<Response>(ItemErrors.Locked);
                }
            }

            DateTime now = clock.UtcNow;

            // Partial send: move the KEPT lines to a NEW draft order (next batch number), so the
            // current order — now holding only the sent lines — keeps its batch number on SENT.
            // This preserves the "Đợt 1, Đợt 2" ordering (sent-now batch is the earlier number).
            if (kept.Count > 0)
            {
                short maxNo = await db.Orders.Where(o => o.TicketId == ticket.Id)
                    .Select(o => (short?)o.OrderNumber).MaxAsync(ct) ?? 0;
                var nextDraft = new Order
                {
                    TicketId = ticket.Id,
                    OrderNumber = (short)(maxNo + 1),
                    Status = OrderStatus.Draft,
                    CreatedByStaffId = currentStaff.StaffAccountId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Orders.Add(nextDraft);
                await db.SaveChangesAsync(ct); // need nextDraft.Id to reassign kept lines

                foreach (CartItem k in kept)
                {
                    k.OrderId = nextDraft.Id;
                    k.UpdatedAt = now;
                }
            }

            var selectedIds = selected.Select(c => c.Id).ToList();
            List<CartItemDetail> cartDetails = await db.CartItemDetails
                .Where(d => selectedIds.Contains(d.CartItemId))
                .ToListAsync(ct);
            var detailsByCart = cartDetails.GroupBy(d => d.CartItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Snapshot each sent item's kitchen station at send time — line items AND set components,
            // so set components get their own per-station kitchen lifecycle.
            var itemIds = selected.Select(c => c.ItemId)
                .Concat(cartDetails.Select(d => d.ItemId))
                .Distinct().ToList();
            var kitchenByItem = await db.Items
                .Where(i => itemIds.Contains(i.Id))
                .Select(i => new { i.Id, i.KitchenStationId })
                .ToListAsync(ct);
            var stationByItem = kitchenByItem.ToDictionary(x => x.Id, x => x.KitchenStationId);

            foreach (CartItem c in selected)
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
                    OriginalOrderItemId = c.OriginalOrderItemId,
                    CancellationReasonId = c.CancellationReasonId,
                    CancellationNote = c.CancellationNote,
                    SentAt = now,
                    Notes = c.Notes,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.OrderItems.Add(orderItem);
                await db.SaveChangesAsync(ct); // need orderItem.Id for its details

                foreach (CartItemDetail d in detailsByCart.GetValueOrDefault(c.Id) ?? [])
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
                        KitchenStationId = stationByItem.GetValueOrDefault(d.ItemId),
                        Status = OrderItemStatus.Pending,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            // Clear the sent lines from the cart and flip the order to SENT.
            db.CartItems.RemoveRange(selected);
            order.Status = OrderStatus.Sent;
            order.SentAt = now;
            order.UpdatedAt = now;

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == currentStaff.StaffAccountId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Order),
                EntityId = order.Id,
                Action = "SEND_KITCHEN",
                ActorStaffAccountId = currentStaff.StaffAccountId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Order #{order.OrderNumber} sent: {selected.Count} items (ticket {ticket.Id})"
            });

            // Refund lines (negative-qty CartItems linked to an original DONE OrderItem) get their own
            // REFUND audit, keyed to the ORIGINAL OrderItem so the original's history shows the refund.
            foreach (CartItem c in selected.Where(c => c.OriginalOrderItemId is not null))
            {
                db.AuditLogs.Add(new AuditLog
                {
                    EntityType = nameof(OrderItem),
                    EntityId = c.OriginalOrderItemId!.Value,
                    Action = "REFUND",
                    ActorStaffAccountId = currentStaff.StaffAccountId,
                    ActorFullName = staff.FullName,
                    Timestamp = now,
                    Summary = $"Refund {Math.Abs(c.Quantity)} x {c.ItemName} (ticket {ticket.Id})"
                });
            }
            await db.SaveChangesAsync(ct);

            await ticketRecompute.RecomputeAsync(ticket.Id, ct);
            await db.SaveChangesAsync(ct);

            // ---- Auto-apply discount ----
            var discountApplied = await TryAutoApplyDiscountAsync(ticket.Id, ct);
            if (discountApplied)
            {
                await ticketRecompute.RecomputeAsync(ticket.Id, ct);
                await db.SaveChangesAsync(ct);
            }

            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Order.Send(ticketId={ticket.Id})", ct);
            await versionService.BumpAsync(VersionScopes.Kitchen, $"Order.Send(ticketId={ticket.Id})", ct);
            if (discountApplied)
            {
                await versionService.BumpAsync(VersionScopes.Pricing, $"Discount.AutoApply(ticketId={ticket.Id})", ct);
            }

            decimal totalAmount = await db.Tickets.Where(t => t.Id == ticket.Id)
                .Select(t => t.TotalAmount).FirstAsync(ct);

            return Result.Success(new Response(order.Id, order.OrderNumber, selected.Count, totalAmount));
        }

        /// <summary>
        /// Evaluate all active auto-apply policies against the ticket, pick the best match,
        /// and apply it. Returns true when a discount was applied (caller must recompute).
        /// </summary>
        private async Task<bool> TryAutoApplyDiscountAsync(long ticketId, CancellationToken ct)
        {
            var policies = await db.DiscountPolicies
                .Where(p => p.IsAutoApply && p.IsActive)
                .Include(p => p.Conditions)
                .ToListAsync(ct);

            if (policies.Count == 0)
            {
                return false;
            }

            var ticket = await db.Tickets
                .Where(t => t.Id == ticketId)
                .Select(t => new { t.Id, t.AreaId, t.Subtotal })
                .FirstAsync(ct);

            var orderItems = await db.OrderItems
                .Where(o => o.TicketId == ticketId && o.Status != OrderItemStatus.Cancelled)
                .ToListAsync(ct);

            var buckets = orderItems
                .GroupBy(o => o.ItemId)
                .Select(g => new DiscountEvaluator.ItemBucket(
                    g.Key, g.Sum(o => o.Quantity), g.Sum(o => o.LineSubtotal)))
                .ToList();

            var today = ((int)clock.UtcNow.DayOfWeek + 6) % 7 + 1; // Mon=1..Sun=7

            DiscountEvaluator.Result? bestEval = null;
            DiscountPolicy? bestPolicy = null;

            foreach (var p in policies)
            {
                var specs = p.Conditions.Select(c => new DiscountEvaluator.ConditionSpec(
                    c.ThresholdAmount, c.ItemId, c.QuantityThreshold,
                    c.AreaId, c.ApplyType, c.DiscountValue)).ToList();

                var r = DiscountEvaluator.Evaluate(
                    p.DiscountType, p.DaysOfWeek, today,
                    ticket.Subtotal, ticket.AreaId, buckets, specs);

                if (r is not null && (bestEval is null || r.DiscountValue > bestEval.DiscountValue))
                {
                    bestEval = r;
                    bestPolicy = p;
                }
            }

            if (bestEval is null || bestPolicy is null)
            {
                return false;
            }

            var ticketEntity = await db.Tickets.FirstAsync(t => t.Id == ticketId, ct);
            ticketEntity.DiscountPolicyId = bestPolicy.Id;

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == currentStaff.StaffAccountId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = ticketId,
                Action = "APPLY_DISCOUNT",
                ActorStaffAccountId = currentStaff.StaffAccountId,
                ActorFullName = staff.FullName,
                Timestamp = clock.UtcNow,
                Summary = $"Auto discount \"{bestPolicy.Code}\" applied: {bestEval.ApplyType} {bestEval.DiscountValue}"
            });

            // Percents derived by TicketRecomputeService; caller recomputes after this returns.
            return true;
        }
    }
}
