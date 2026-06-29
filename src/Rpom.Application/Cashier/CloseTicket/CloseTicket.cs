using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;

namespace Rpom.Application.Cashier.CloseTicket;
public static class CloseTicket
{
    public sealed record Command(long TicketId) : ICommand<Response>;

    public sealed record Response(
        long TicketId,
        string Status,
        string TableStatus,
        IReadOnlyList<long> CancelledOrderIds);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);
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

            bool drawerOpen = await db.CashDrawerSessions
                .AnyAsync(d => d.CounterId == ticket.CounterId && d.Status == CashDrawerStatus.Open, ct);
            if (!drawerOpen) return Result.Failure<Response>(TicketErrors.NoOpenCashDrawer);

            bool hasPending = await db.TicketPaymentDetails
                .AnyAsync(p => p.TicketId == ticket.Id && p.Status == TicketPaymentStatus.Pending, ct);
            if (hasPending) return Result.Failure<Response>(PaymentErrors.PendingPaymentExists);

            if (ticket.TotalAmount - ticket.PaidAmount > 0)
                return Result.Failure<Response>(TicketErrors.NotFullyPaid);

            var openOrders = await db.Orders
                .Where(o => o.TicketId == ticket.Id
                            && o.Status != OrderStatus.Done
                            && o.Status != OrderStatus.Deleted)
                .ToListAsync(ct);

            // PROCESSING order
            if (openOrders.Any(o => o.Status == OrderStatus.Processing))
                return Result.Failure<Response>(TicketErrors.HasProcessingOrder);

            DateTime now = clock.UtcNow;
            var cancelledOrderIds = new List<long>();

            // DRAFT/SENT order
            var ordersToCancel = openOrders
                .Where(o => o.Status == OrderStatus.Draft || o.Status == OrderStatus.Sent)
                .ToList();

            if (ordersToCancel.Count > 0)
            {
                var cancelOrderIds = ordersToCancel.Select(o => o.Id).ToList();

                var pendingItems = await db.OrderItems
                    .Where(oi => cancelOrderIds.Contains(oi.OrderId) && oi.Status == OrderItemStatus.Pending)
                    .ToListAsync(ct);
                foreach (var oi in pendingItems)
                {
                    oi.Status = OrderItemStatus.Cancelled;
                    oi.CancelledAt = now;
                    oi.CancelledByStaffId = staffId;
                    oi.UpdatedAt = now;
                }

                foreach (var o in ordersToCancel)
                {
                    o.Status = OrderStatus.Deleted;
                    o.UpdatedAt = now;
                    cancelledOrderIds.Add(o.Id);
                }
            }

            ticket.Status = TicketStatus.Closed;
            ticket.ClosedAt = now;
            ticket.UpdatedAt = now;

            bool otherOpenTicket = await db.Tickets
                .AnyAsync(t => t.TableId == ticket.TableId && t.Id != ticket.Id && t.Status == TicketStatus.Open, ct);

            Table tableRow = await db.Tables.FirstAsync(t => t.Id == ticket.TableId, ct);
            if (!otherOpenTicket)
            {
                tableRow.Status = TableStatus.Available;
                tableRow.UpdatedAt = now;
            }
            string tableStatus = tableRow.Status;

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);

            // Create immutable invoice snapshot
            var itemSums = await db.TicketItemSums
                .Where(ts => ts.TicketId == ticket.Id)
                .OrderBy(ts => ts.DisplayOrder)
                .ToListAsync(ct);

            var invoice = new TicketInvoice
            {
                TicketId = ticket.Id,
                TicketCode = ticket.Code,
                CounterId = ticket.CounterId,
                AreaId = ticket.AreaId,
                ShiftId = ticket.ShiftId,
                TableId = ticket.TableId,
                TableCode = tableRow.Code,
                GuestCount = ticket.GuestCount,
                WaiterStaffId = ticket.WaiterStaffId,
                WaiterName = ticket.WaiterStaff?.FullName,
                ClosedByStaffId = staffId,
                ClosedByName = staff.FullName,
                Subtotal = ticket.Subtotal,
                DiscountAmount = ticket.DiscountAmount,
                DiscountPercent = ticket.DiscountPercent,
                ServiceChargeAmount = ticket.ServiceChargeAmount,
                ServiceChargePercent = ticket.ServiceChargePercent,
                VatAmount = ticket.VatAmount,
                TotalAmount = ticket.TotalAmount,
                RoundingAdjustment = ticket.RoundingAdjustment,
                PaidAmount = ticket.PaidAmount,
                RefundAmount = ticket.RefundAmount,
                ChangeAmount = ticket.ChangeAmount,
                OpenedAt = ticket.OpenedAt,
                ClosedAt = now,
                CreatedAt = now,
                Lines = itemSums.Select(ts => new TicketInvoiceLine
                {
                    ItemId = ts.ItemId,
                    ItemCode = ts.ItemCode,
                    ItemName = ts.ItemName,
                    UomCode = ts.UomCode,
                    UomName = ts.UomName,
                    UnitPrice = ts.UnitPrice,
                    ChoicePricePerUnit = ts.ChoicePricePerUnit,
                    Quantity = ts.TotalQuantity,
                    VatPercent = ts.VatPercent,
                    ServiceChargePercent = ts.ServiceChargePercent,
                    ServiceChargeVatPercent = ts.ServiceChargeVatPercent,
                    LineDiscountPercent = ts.LineDiscountPercent,
                    TicketDiscountPercent = ts.TicketDiscountPercent,
                    LineSubtotal = ts.TotalLineSubtotal,
                    TotalDiscount = ts.TotalDiscount,
                    ServiceChargeAmount = ts.TotalServiceCharge,
                    VatAmount = ts.TotalVat,
                    TotalAmount = ts.TotalAmount,
                    DisplayOrder = ts.DisplayOrder,
                    CreatedAt = now
                }).ToList()
            };
            db.TicketInvoices.Add(invoice);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = ticket.Id,
                Action = "CLOSE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Ticket closed: {ticket.Code}"
                          + (cancelledOrderIds.Count > 0 ? $" | auto-cancelled orders: {string.Join(", ", cancelledOrderIds)}" : "")
                          + $" | table {ticket.TableId} → {tableStatus}",
            });

            await db.SaveChangesAsync(ct);

            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Ticket.Close(id={ticket.Id})", ct);
            if (cancelledOrderIds.Count > 0)
                await versionService.BumpAsync(VersionScopes.Kitchen, $"Ticket.Close(id={ticket.Id})", ct);

            return Result.Success(new Response(ticket.Id, ticket.Status, tableStatus, cancelledOrderIds));
        }
    }
}
