using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Configuration;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;

namespace Rpom.Application.Cashier.MergeTicket;
public static class MergeTicket
{
    public sealed record Command(long SourceTicketId, long DestinationTicketId) : ICommand<Response>;

    public sealed record Response(
        long SourceTicketId,
        string SourceStatus,
        long DestinationTicketId,
        int MovedOrderCount,
        int MovedPaymentCount,
        long DestinationTotalAmount,
        long DestinationPaidAmount);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.SourceTicketId).GreaterThan(0);
            RuleFor(x => x.DestinationTicketId).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        ITableOperationGuard guard,
        IConfigValueService config,
        ITicketRecomputeService ticketRecompute,
        IRefreshPaymentTotalsService refreshPayments,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            if (request.SourceTicketId == request.DestinationTicketId)
                return Result.Failure<Response>(TicketErrors.MergeSameTicket);

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            Ticket? source = await db.Tickets.FirstOrDefaultAsync(t => t.Id == request.SourceTicketId, ct);
            if (source is null) return Result.Failure<Response>(TicketErrors.NotFound);
            Ticket? dest = await db.Tickets.FirstOrDefaultAsync(t => t.Id == request.DestinationTicketId, ct);
            if (dest is null) return Result.Failure<Response>(TicketErrors.NotFound);

            if (source.Status != TicketStatus.Open || dest.Status != TicketStatus.Open)
                return Result.Failure<Response>(TicketErrors.NotOpen);

            if (source.AreaId != dest.AreaId)
                return Result.Failure<Response>(TicketErrors.MergeDifferentArea);

            // Cash Drawer Session is OPEN (same area ==> same counter)
            bool drawerOpen = await db.CashDrawerSessions
                .AnyAsync(d => d.CounterId == dest.CounterId && d.Status == CashDrawerStatus.Open, ct);
            if (!drawerOpen) return Result.Failure<Response>(TicketErrors.NoOpenCashDrawer);

            // Do not merge if there are pending payments (e.g., unconfirmed QR) on either ticket
            bool anyPending = await db.TicketPaymentDetails
                .AnyAsync(p => (p.TicketId == source.Id || p.TicketId == dest.Id)
                               && p.Status == TicketPaymentStatus.Pending, ct);
            if (anyPending) return Result.Failure<Response>(PaymentErrors.PendingPaymentExists);

            // lock source table before merge
            Result heldSource = await guard.EnsureHeldAsync(source.TableId, staffId, ct);
            if (heldSource.IsFailure) return Result.Failure<Response>(heldSource.Error);

            // DESTINATIOn table (ticket): If currently locked by ANOTHER staff member (active lock), prevent merging
            // (Available / old lock expired / locked by current staff -> allow)
            if (dest.TableId != source.TableId)
            {
                int ttl = await config.GetIntAsync(
                    ConfigCodes.TableLockTtlSeconds, ITableOperationGuard.DefaultTtlSeconds, ct);
                DateTime cutoff = now.AddSeconds(-ttl);
                TableLock? destLock = await db.TableLocks
                    .FirstOrDefaultAsync(l => l.TableId == dest.TableId, ct);
                if (destLock is not null
                    && destLock.StaffAccountId != staffId
                    && destLock.LastHeartbeatAt >= cutoff)
                {
                    return Result.Failure<Response>(TableLockErrors.HeldByOther(destLock.StaffName));
                }
            }

            // Load source data to be transferred (keep reference for cloning later)
            var orders = await db.Orders
                .Where(o => o.TicketId == source.Id
                            && (o.Status == OrderStatus.Sent
                                || o.Status == OrderStatus.Processing
                                || o.Status == OrderStatus.Done))
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Details)
                .ToListAsync(ct);

            var payments = await db.TicketPaymentDetails
                .Where(p => p.TicketId == source.Id && p.Status != TicketPaymentStatus.Deleted)
                .ToListAsync(ct);

            // Renumber the incoming order batches: sequentially continuing from the highest batch number currently existing in the target
            short maxDestOrderNo = await db.Orders
                .Where(o => o.TicketId == dest.Id)
                .Select(o => (short?)o.OrderNumber)
                .MaxAsync(ct) ?? (short)0;
            short nextOrderNo = (short)(maxDestOrderNo + 1);

            // --- B1: chuyển bản gốc sang hoá đơn đích (chỉ đổi TicketId) ---
            foreach (Order o in orders.OrderBy(o => o.OrderNumber).ThenBy(o => o.Id))
            {
                o.TicketId = dest.Id;
                o.OrderNumber = nextOrderNo++;
                o.UpdatedAt = now;
                foreach (OrderItem oi in o.OrderItems)
                {
                    oi.TicketId = dest.Id;
                    oi.UpdatedAt = now;
                }
            }
            foreach (TicketPaymentDetail p in payments)
            {
                p.TicketId = dest.Id;
                p.UpdatedAt = now;
            }

            await db.SaveChangesAsync(ct); // commit moves so recompute (DB query) thấy được

            // --- B2: tính lại tiền cho cả hai hoá đơn ---
            await ticketRecompute.RecomputeAsync(dest.Id, ct);
            await refreshPayments.RefreshAsync(dest.Id, ct);
            await ticketRecompute.RecomputeAsync(source.Id, ct);   // nguồn còn lại 0 (đã chuyển hết)
            await refreshPayments.RefreshAsync(source.Id, ct);

            // --- B3: tạo bản copy huỷ trên hoá đơn nguồn (lưu vết) ---
            // Bản copy cần OrderNumber DUY NHẤT trong hoá đơn nguồn (constraint ux_order_ticket_sequence)
            short maxSrcOrderNo = await db.Orders
                .Where(o => o.TicketId == source.Id)
                .Select(o => (short?)o.OrderNumber)
                .MaxAsync(ct) ?? (short)0;
            short nextSrcOrderNo = (short)(maxSrcOrderNo + 1);

            foreach (Order o in orders)
            {
                var orderCopy = new Order
                {
                    TicketId = source.Id,
                    OrderNumber = nextSrcOrderNo++,
                    Status = OrderStatus.Deleted, // DELETED order
                    SentAt = o.SentAt,
                    CreatedByStaffId = o.CreatedByStaffId,
                    Notes = o.Notes,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.Orders.Add(orderCopy);

                foreach (OrderItem oi in o.OrderItems)
                {
                    var itemCopy = new OrderItem
                    {
                        Order = orderCopy,
                        TicketId = source.Id,
                        ItemId = oi.ItemId,
                        ItemCode = oi.ItemCode,
                        ItemName = oi.ItemName,
                        UomId = oi.UomId,
                        UomCode = oi.UomCode,
                        UomName = oi.UomName,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        ChoicePricePerUnit = oi.ChoicePricePerUnit,
                        VatPercent = oi.VatPercent,
                        ServiceChargePercent = oi.ServiceChargePercent,
                        ServiceChargeVatPercent = oi.ServiceChargeVatPercent,
                        LineSubtotal = oi.LineSubtotal,
                        LineDiscountPercent = oi.LineDiscountPercent,
                        LineDiscountAmount = oi.LineDiscountAmount,
                        TicketDiscountPercent = oi.TicketDiscountPercent,
                        TicketDiscountAmount = oi.TicketDiscountAmount,
                        TotalDiscountAmount = oi.TotalDiscountAmount,
                        ServiceChargeAmount = oi.ServiceChargeAmount,
                        VatItemAmount = oi.VatItemAmount,
                        VatScAmount = oi.VatScAmount,
                        VatAmount = oi.VatAmount,
                        LineTotal = oi.LineTotal,
                        KitchenStationId = oi.KitchenStationId,
                        Status = OrderItemStatus.Cancelled,
                        SentAt = oi.SentAt,
                        CancelledAt = now,
                        CancelledByStaffId = staffId,
                        CancellationNote = $"Merged into ticket {dest.Code}",
                        Notes = oi.Notes,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    db.OrderItems.Add(itemCopy);

                    foreach (OrderItemDetail d in oi.Details)
                    {
                        db.OrderItemDetails.Add(new OrderItemDetail
                        {
                            OrderItem = itemCopy,
                            ChoiceCategoryId = d.ChoiceCategoryId,
                            ItemId = d.ItemId,
                            ItemName = d.ItemName,
                            ComponentType = d.ComponentType,
                            Quantity = d.Quantity,
                            ExtraPrice = d.ExtraPrice,
                            Notes = d.Notes,
                            CreatedAt = now,
                        });
                    }
                }
            }

            // Payment copy (CANCELLED) — giữ liên kết cha-con (dòng tiền thối) trong cụm copy
            var paymentCopyByOldId = new Dictionary<long, TicketPaymentDetail>();
            foreach (TicketPaymentDetail p in payments)
            {
                var copy = new TicketPaymentDetail
                {
                    TicketId = source.Id,
                    PaymentMethodId = p.PaymentMethodId,
                    Amount = p.Amount,
                    Status = TicketPaymentStatus.Cancelled,
                    ProcessedAt = now,
                    ProcessedByStaffId = p.ProcessedByStaffId,
                    TransactionRef = null, // bản copy huỷ không mang vendor tx ref
                    Notes = p.Notes,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.TicketPaymentDetails.Add(copy);
                paymentCopyByOldId[p.Id] = copy;
            }
            foreach (TicketPaymentDetail p in payments.Where(p => p.ParentPaymentDetailId is not null))
            {
                if (paymentCopyByOldId.TryGetValue(p.Id, out var childCopy)
                    && paymentCopyByOldId.TryGetValue(p.ParentPaymentDetailId!.Value, out var parentCopy))
                {
                    childCopy.ParentPaymentDetail = parentCopy;
                }
            }

            // Cộng dồn số khách của nguồn vào đích.
            dest.GuestCount = (short)(dest.GuestCount + source.GuestCount);
            dest.UpdatedAt = now;

            // --- B4: huỷ hoá đơn nguồn + trả bàn ---
            int? mergeReasonId = await db.CancellationReasons
                .Where(r => r.Code == "MERGE" && r.IsActive)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync(ct);

            source.Status = TicketStatus.Cancelled;
            source.CancelledAt = now;
            source.CancellationReasonId = mergeReasonId;
            source.CancellationNote = $"Merged into ticket {dest.Code}";
            source.UpdatedAt = now;

            bool otherOpenOnSourceTable = await db.Tickets
                .AnyAsync(t => t.TableId == source.TableId && t.Id != source.Id && t.Status == TicketStatus.Open, ct);
            Table sourceTable = await db.Tables.FirstAsync(t => t.Id == source.TableId, ct);
            if (!otherOpenOnSourceTable)
            {
                sourceTable.Status = TableStatus.Available;
                sourceTable.UpdatedAt = now;
            }

            // Nhả khoá thao tác của bàn nguồn (ticket nguồn đã CANCELLED, không thao tác tiếp).
            // Giữ lại khoá bàn đích vì cashier còn làm việc trên hoá đơn đích.
            TableLock? sourceLock = await db.TableLocks
                .FirstOrDefaultAsync(l => l.TableId == source.TableId && l.StaffAccountId == staffId, ct);
            if (sourceLock is not null)
                db.TableLocks.Remove(sourceLock);

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = source.Id,
                Action = "MERGE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Merged ticket {source.Code} → {dest.Code} | {orders.Count} order(s), {payments.Count} payment(s)",
            });
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = dest.Id,
                Action = "MERGE_RECEIVE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Received merged ticket {source.Code} | {orders.Count} order(s), {payments.Count} payment(s)",
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<Response>(PaymentErrors.ConcurrencyConflict);
            }

            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Ticket.Merge(src={source.Id},dest={dest.Id})", ct);
            await versionService.BumpAsync(VersionScopes.Kitchen, $"Ticket.Merge(src={source.Id},dest={dest.Id})", ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"Ticket.Merge(src={source.Id},dest={dest.Id})", ct);

            return Result.Success(new Response(
                source.Id, source.Status,
                dest.Id,
                orders.Count, payments.Count,
                (long)dest.TotalAmount, (long)dest.PaidAmount));
        }
    }
}
