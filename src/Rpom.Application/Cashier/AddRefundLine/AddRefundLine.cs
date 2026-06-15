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

namespace Rpom.Application.Cashier.AddRefundLine;

/// <summary>
///     Add a refund (return) line for an already-cooked order item. Creates a negative-quantity
///     DRAFT cart line snapshotted from the original OrderItem and linked via OriginalOrderItemId.
///     The cashier then sends it through the normal SendOrder path. PENDING originals use Cancel,
///     not refund. Reason is required.
/// </summary>
public static class AddRefundLine
{
    public sealed record Command(
        long TicketId, long OrderItemId, decimal Quantity, int CancellationReasonId, string? CancellationNote)
        : ICommand<Response>;

    public sealed record Response(long CartItemId, long OrderId, decimal Quantity);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);
            RuleFor(x => x.OrderItemId).GreaterThan(0);
            RuleFor(x => x.Quantity).GreaterThan(0);
            RuleFor(x => x.CancellationReasonId).GreaterThan(0);
            RuleFor(x => x.CancellationNote).MaximumLength(500);
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

            Result held = await guard.EnsureHeldAsync(ticket.TableId, currentStaff.StaffAccountId, ct);
            if (held.IsFailure) return Result.Failure<Response>(held.Error);

            OrderItem? original = await db.OrderItems
                .FirstOrDefaultAsync(o => o.Id == request.OrderItemId && o.TicketId == ticket.Id, ct);
            if (original is null) return Result.Failure<Response>(OrderItemErrors.WrongTicket);

            if (original.Quantity <= 0m || original.OriginalOrderItemId is not null)
                return Result.Failure<Response>(OrderItemErrors.CannotRefundRefund);

            if (original.Status != OrderItemStatus.Processing
                && original.Status != OrderItemStatus.Ready
                && original.Status != OrderItemStatus.Done)
                return Result.Failure<Response>(OrderItemErrors.NotRefundable);

            var reason = await db.CancellationReasons
                .Where(r => r.Id == request.CancellationReasonId)
                .Select(r => new { r.IsActive })
                .FirstOrDefaultAsync(ct);
            if (reason is null || !reason.IsActive)
                return Result.Failure<Response>(TicketErrors.InvalidCancellationReason);

            // Remaining = original qty minus everything already refunded, across BOTH already-sent
            // refund OrderItems and still-draft refund CartItems (all carry negative quantities).
            decimal committedRefunded = -(await db.OrderItems
                .Where(o => o.OriginalOrderItemId == original.Id)
                .SumAsync(o => (decimal?)o.Quantity, ct) ?? 0m);
            decimal draftRefunded = -(await db.CartItems
                .Where(c => c.OriginalOrderItemId == original.Id)
                .SumAsync(c => (decimal?)c.Quantity, ct) ?? 0m);
            decimal remaining = original.Quantity - committedRefunded - draftRefunded;
            if (request.Quantity > remaining)
                return Result.Failure<Response>(OrderItemErrors.RefundQuantityExceeded);

            DateTime now = clock.UtcNow;

            Order? order = await db.Orders
                .FirstOrDefaultAsync(o => o.TicketId == ticket.Id && o.Status == OrderStatus.Draft, ct);
            if (order is null)
            {
                short maxNo = await db.Orders.Where(o => o.TicketId == ticket.Id)
                    .Select(o => (short?)o.OrderNumber).MaxAsync(ct) ?? 0;
                order = new Order
                {
                    TicketId = ticket.Id,
                    OrderNumber = (short)(maxNo + 1),
                    Status = OrderStatus.Draft,
                    CreatedByStaffId = currentStaff.StaffAccountId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Orders.Add(order);
                await db.SaveChangesAsync(ct);
            }

            var cartItem = new CartItem
            {
                OrderId = order.Id,
                ItemId = original.ItemId,
                ItemCode = original.ItemCode,
                ItemName = original.ItemName,
                UomId = original.UomId,
                UomCode = original.UomCode,
                UomName = original.UomName,
                Quantity = -request.Quantity,
                UnitPrice = original.UnitPrice,
                ChoicePricePerUnit = original.ChoicePricePerUnit,
                VatPercent = original.VatPercent,
                ServiceChargePercent = original.ServiceChargePercent,
                ServiceChargeVatPercent = original.ServiceChargeVatPercent,
                OriginalOrderItemId = original.Id,
                CancellationReasonId = request.CancellationReasonId,
                CancellationNote = request.CancellationNote,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.CartItems.Add(cartItem);
            await db.SaveChangesAsync(ct);

            await cartRecompute.RecomputeAsync(order.Id, ct);
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Refund.AddLine(ticketId={ticket.Id})", ct);

            return Result.Success(new Response(cartItem.Id, order.Id, cartItem.Quantity));
        }
    }
}
