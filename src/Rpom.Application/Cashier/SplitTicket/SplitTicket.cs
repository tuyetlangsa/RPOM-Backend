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

namespace Rpom.Application.Cashier.SplitTicket;

/// <summary>
///     Tách món từ hoá đơn nguồn sang hoá đơn đích (cùng khu vực).
///     <para>Điều kiện:</para>
///     <list type="bullet">
///         <item>Hoá đơn nguồn tồn tại + OPEN.</item>
///         <item>Đích: hoặc <c>DestinationTicketId</c> (có sẵn, OPEN, cùng area) HOẶC
///             <c>DestinationTableId</c> (mở hoá đơn đích mới trên bàn cùng area).</item>
///         <item>Quầy của khu vực đang có ca tiền mặt (CashDrawerSession) OPEN.</item>
///         <item>Không hoá đơn nào còn payment PENDING.</item>
///     </list>
///     <para>Tách: chuyển từng món với số lượng ≤ số lượng hiện có ở nguồn.
///     Chuyển toàn bộ số lượng → re-point cả dòng; chuyển một phần → giảm số lượng ở nguồn
///     và tạo dòng mới ở đích. Món giữ nguyên trạng thái bếp. Đợt ở nguồn bị tách hết món
///     active sẽ chuyển DELETED. Tính lại tiền cho cả hai hoá đơn.</para>
/// </summary>
public static class SplitTicket
{
    public sealed record SplitItemInput(long OrderItemId, decimal Quantity);

    public sealed record Command(
        long SourceTicketId,
        long? DestinationTicketId,
        int? DestinationTableId,
        short? GuestCount,
        IReadOnlyList<SplitItemInput> Items) : ICommand<Response>;

    public sealed record Response(
        long SourceTicketId,
        long DestinationTicketId,
        string DestinationTicketCode,
        bool DestinationCreated,
        int MovedItemCount,
        long SourceTotalAmount,
        long DestinationTotalAmount);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.SourceTicketId).GreaterThan(0);
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items).ChildRules(i =>
            {
                i.RuleFor(x => x.OrderItemId).GreaterThan(0);
                i.RuleFor(x => x.Quantity).GreaterThan(0);
            });
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
            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            // Just have one of DestinationTicketId and DestinationTableId, not both
            bool hasDestTicket = request.DestinationTicketId is > 0;
            bool hasDestTable = request.DestinationTableId is > 0;
            if (hasDestTicket == hasDestTable)
                return Result.Failure<Response>(TicketErrors.SplitDestinationInvalid);

            // Item not duplicate + quantity > 0
            var moveById = new Dictionary<long, decimal>();
            foreach (var it in request.Items)
            {
                if (it.Quantity <= 0) return Result.Failure<Response>(TicketErrors.SplitItemInvalid);
                if (!moveById.TryAdd(it.OrderItemId, it.Quantity))
                    return Result.Failure<Response>(TicketErrors.SplitItemInvalid);  // dupliacate OrderItemId
            }
            if (moveById.Count == 0) return Result.Failure<Response>(TicketErrors.SplitNoItems);

            Ticket? source = await db.Tickets.FirstOrDefaultAsync(t => t.Id == request.SourceTicketId, ct);
            if (source is null) return Result.Failure<Response>(TicketErrors.NotFound);
            if (source.Status != TicketStatus.Open) return Result.Failure<Response>(TicketErrors.NotOpen);

            // Current staff hold source table
            Result heldSource = await guard.EnsureHeldAsync(source.TableId, staffId, ct);
            if (heldSource.IsFailure) return Result.Failure<Response>(heldSource.Error);

            // Cash Drawer Ss OPEN
            bool drawerOpen = await db.CashDrawerSessions
                .AnyAsync(d => d.CounterId == source.CounterId && d.Status == CashDrawerStatus.Open, ct);
            if (!drawerOpen) return Result.Failure<Response>(TicketErrors.NoOpenCashDrawer);

            // Not PENDING payment on source
            bool srcPending = await db.TicketPaymentDetails
                .AnyAsync(p => p.TicketId == source.Id && p.Status == TicketPaymentStatus.Pending, ct);
            if (srcPending) return Result.Failure<Response>(PaymentErrors.PendingPaymentExists);

            // Must not have any PAID amount on source (otherwise split will lose money)
            if (source.PaidAmount > 0)
                return Result.Failure<Response>(TicketErrors.SplitSourcePaid);

            // Validate split items: must exist on source, not cancelled, quantity > 0, move quantity ≤ current quantity
            var requestedIds = moveById.Keys.ToList();
            var items = await db.OrderItems
                .Include(oi => oi.Details)
                .Where(oi => requestedIds.Contains(oi.Id))
                .ToListAsync(ct);

            if (items.Count != requestedIds.Count)
                return Result.Failure<Response>(TicketErrors.SplitItemInvalid);
            foreach (var oi in items)
            {
                if (oi.TicketId != source.Id
                    || oi.Status == OrderItemStatus.Cancelled
                    || oi.Quantity <= 0)
                    return Result.Failure<Response>(TicketErrors.SplitItemInvalid);
                if (moveById[oi.Id] > oi.Quantity)
                    return Result.Failure<Response>(TicketErrors.SplitQuantityExceeds);
            }

            // Open destination ticket (existing or new) and validate: OPEN, same area, no PENDING payment, not held by other staff
            Ticket dest;
            bool destCreated = false;
            if (hasDestTicket)
            {
                Ticket? existingDest = await db.Tickets
                    .FirstOrDefaultAsync(t => t.Id == request.DestinationTicketId!.Value, ct);
                if (existingDest is null) return Result.Failure<Response>(TicketErrors.NotFound);
                if (existingDest.Id == source.Id) return Result.Failure<Response>(TicketErrors.SplitSameTicket);
                if (existingDest.Status != TicketStatus.Open) return Result.Failure<Response>(TicketErrors.NotOpen);
                if (existingDest.AreaId != source.AreaId) return Result.Failure<Response>(TicketErrors.SplitDifferentArea);

                bool destPending = await db.TicketPaymentDetails
                    .AnyAsync(p => p.TicketId == existingDest.Id && p.Status == TicketPaymentStatus.Pending, ct);
                if (destPending) return Result.Failure<Response>(PaymentErrors.PendingPaymentExists);

                string? destHolder = await OtherHolderAsync(existingDest.TableId);
                if (destHolder is not null)
                    return Result.Failure<Response>(TableLockErrors.HeldByOther(destHolder));

                dest = existingDest;
            }
            else
            {
                var tableInfo = await db.Tables
                    .Where(t => t.Id == request.DestinationTableId!.Value && t.IsActive)
                    .Select(t => new
                    {
                        t.Id, t.AreaId, t.Area.CounterId,
                        t.Area.ServiceChargePercent, t.Area.ServiceChargeVatPercent
                    })
                    .FirstOrDefaultAsync(ct);
                if (tableInfo is null) return Result.Failure<Response>(TableErrors.NotFound);
                if (tableInfo.AreaId != source.AreaId) return Result.Failure<Response>(TicketErrors.SplitDifferentArea);
                string? destTableHolder = await OtherHolderAsync(tableInfo.Id);
                if (destTableHolder is not null)
                    return Result.Failure<Response>(TableLockErrors.HeldByOther(destTableHolder));

                var drawer = await db.CashDrawerSessions
                    .Where(d => d.CounterId == tableInfo.CounterId && d.Status == CashDrawerStatus.Open)
                    .Select(d => new { d.Id, d.ShiftId })
                    .FirstOrDefaultAsync(ct);
                if (drawer is null) return Result.Failure<Response>(TicketErrors.NoOpenCashDrawer);

                // Create new ticket on destination table (OPEN, no payment yet)
                dest = new Ticket
                {
                    Code = "TK-PENDING", // thay bằng TK-{date}-{id} sau lần save đầu
                    TableId = tableInfo.Id,
                    AreaId = tableInfo.AreaId,
                    CounterId = tableInfo.CounterId,
                    CashDrawerSessionId = drawer.Id,
                    ShiftId = drawer.ShiftId,
                    GuestCount = request.GuestCount is > 0 ? request.GuestCount.Value : (short)1,
                    WaiterStaffId = staffId,
                    Status = TicketStatus.Open,
                    OpenedAt = now,
                    ServiceChargePercent = tableInfo.ServiceChargePercent,
                    ServiceChargeVatPercent = tableInfo.ServiceChargeVatPercent,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.Tickets.Add(dest);
                await db.SaveChangesAsync(ct); // cần dest.Id để sinh mã (an toàn: đã qua hết validation)
                dest.Code = $"TK-{now:yyyyMMdd}-{dest.Id}";
                destCreated = true;

                Table destTableRow = await db.Tables.FirstAsync(t => t.Id == tableInfo.Id, ct);
                destTableRow.Status = TableStatus.Occupied;
                destTableRow.UpdatedAt = now;
            }

            // The order source is affected (check for emptiness after splitting).
            var affectedSourceOrderIds = items.Select(i => i.OrderId).Distinct().ToList();

            // Create a separate order on the destination ticket to contain the item being transferred
            short maxDestOrderNo = await db.Orders
                .Where(o => o.TicketId == dest.Id)
                .Select(o => (short?)o.OrderNumber).MaxAsync(ct) ?? (short)0;

            var splitOrder = new Order
            {
                Ticket = dest,
                OrderNumber = (short)(maxDestOrderNo + 1),
                Status = OrderStatus.Sent, // sẽ roll-up theo trạng thái món bên dưới
                SentAt = now,
                CreatedByStaffId = staffId,
                Notes = $"Tách từ hoá đơn {source.Code}",
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Orders.Add(splitOrder);

            var movedStatuses = new List<string>();
            foreach (var oi in items)
            {
                decimal m = moveById[oi.Id];
                movedStatuses.Add(oi.Status);

                if (m >= oi.Quantity)
                {
                    // Chuyển toàn bộ dòng: re-parent sang đợt nhận của đích.
                    oi.Order = splitOrder;
                    oi.Ticket = dest;
                    oi.UpdatedAt = now;
                }
                else
                {
                    // Chuyển một phần: giảm ở nguồn, tạo dòng mới ở đích.
                    oi.Quantity -= m;
                    oi.UpdatedAt = now;

                    var copy = new OrderItem
                    {
                        Order = splitOrder,
                        Ticket = dest,
                        ItemId = oi.ItemId,
                        ItemCode = oi.ItemCode,
                        ItemName = oi.ItemName,
                        UomId = oi.UomId,
                        UomCode = oi.UomCode,
                        UomName = oi.UomName,
                        Quantity = m,
                        UnitPrice = oi.UnitPrice,
                        ChoicePricePerUnit = oi.ChoicePricePerUnit,
                        VatPercent = oi.VatPercent,
                        ServiceChargePercent = oi.ServiceChargePercent,
                        ServiceChargeVatPercent = oi.ServiceChargeVatPercent,
                        LineDiscountPercent = oi.LineDiscountPercent,
                        TicketDiscountPercent = oi.TicketDiscountPercent,
                        KitchenStationId = oi.KitchenStationId,
                        Status = oi.Status,
                        SentAt = oi.SentAt,
                        StartCookAt = oi.StartCookAt,
                        ReadyAt = oi.ReadyAt,
                        DoneAt = oi.DoneAt,
                        Notes = oi.Notes,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    db.OrderItems.Add(copy);

                    foreach (var d in oi.Details)
                    {
                        db.OrderItemDetails.Add(new OrderItemDetail
                        {
                            OrderItem = copy,
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

            // Roll-up trạng thái đợt nhận theo trạng thái món đã tách.
            splitOrder.Status = RollupOrderStatus(movedStatuses);

            await db.SaveChangesAsync(ct); // commit moves

            // Update the status of affected source batches:
            //  - No active items left → DELETED
            //  - Active items remaining → Roll up based on the status of remaining items (to avoid being stuck in legacy PROCESSING/SENT status)
            foreach (long oid in affectedSourceOrderIds)
            {
                var remainingStatuses = await db.OrderItems
                    .Where(oi => oi.OrderId == oid && oi.TicketId == source.Id
                                 && oi.Status != OrderItemStatus.Cancelled)
                    .Select(oi => oi.Status)
                    .ToListAsync(ct);

                Order srcOrder = await db.Orders.FirstAsync(o => o.Id == oid, ct);
                if (srcOrder.Status == OrderStatus.Deleted) continue;

                srcOrder.Status = remainingStatuses.Count == 0
                    ? OrderStatus.Deleted
                    : RollupOrderStatus(remainingStatuses);
                srcOrder.UpdatedAt = now;
            }

            // Recalculate total (payments do not move — items only)
            await ticketRecompute.RecomputeAsync(dest.Id, ct);
            await refreshPayments.RefreshAsync(dest.Id, ct);
            await ticketRecompute.RecomputeAsync(source.Id, ct);
            await refreshPayments.RefreshAsync(source.Id, ct);

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = source.Id,
                Action = "SPLIT",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Split {items.Count} item(s) from {source.Code} → {dest.Code}"
                          + (destCreated ? " (new ticket)" : ""),
            });

            // Không bắt DbUpdateConcurrencyException ở đây: để exception lan ra cho
            // TransactionPipelineBehavior rollback toàn bộ (không để lại dữ liệu nửa vời).
            await db.SaveChangesAsync(ct);

            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Ticket.Split(src={source.Id},dest={dest.Id})", ct);
            await versionService.BumpAsync(VersionScopes.Kitchen, $"Ticket.Split(src={source.Id},dest={dest.Id})", ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"Ticket.Split(src={source.Id},dest={dest.Id})", ct);

            return Result.Success(new Response(
                source.Id, dest.Id, dest.Code, destCreated,
                items.Count, (long)source.TotalAmount, (long)dest.TotalAmount));

            async Task<string?> OtherHolderAsync(int tableId)
            {
                if (tableId == source.TableId) return null; // Currently holding the lock on the source table
                int ttl = await config.GetIntAsync(
                    ConfigCodes.TableLockTtlSeconds, ITableOperationGuard.DefaultTtlSeconds, ct);
                DateTime cutoff = now.AddSeconds(-ttl);
                TableLock? l = await db.TableLocks.FirstOrDefaultAsync(x => x.TableId == tableId, ct);
                return l is not null && l.StaffAccountId != staffId && l.LastHeartbeatAt >= cutoff
                    ? l.StaffName
                    : null;
            }
        }

        private static string RollupOrderStatus(IReadOnlyCollection<string> itemStatuses)
        {
            if (itemStatuses.Count == 0) return OrderStatus.Sent;
            if (itemStatuses.All(s => s == OrderItemStatus.Done || s == OrderItemStatus.Cancelled))
                return OrderStatus.Done;
            if (itemStatuses.Any(s => s == OrderItemStatus.Processing || s == OrderItemStatus.Ready))
                return OrderStatus.Processing;
            return OrderStatus.Sent;
        }
    }
}
