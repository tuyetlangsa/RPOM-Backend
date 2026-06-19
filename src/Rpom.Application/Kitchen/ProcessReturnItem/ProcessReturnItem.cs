using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Inventory;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;
using Rpom.Domain.Sales;

namespace Rpom.Application.Kitchen.ProcessReturnItem;

/// <summary>
///     Bếp xử lý một dòng TRẢ HÀNG (refund OrderItem, Quantity âm) thuộc khu bếp của mình:
///     đánh dấu hoàn tất (DONE) và <b>tuỳ chọn hoàn nguyên liệu/hàng vào tồn</b>.
///     <list type="bullet">
///         <item>Chỉ áp dụng cho dòng refund (Quantity &lt; 0, có OriginalOrderItemId).</item>
///         <item>Chỉ bếp đúng station (theo claim) mới xử lý được.</item>
///         <item><c>Restock = true</c> → cộng lại kho: món công thức trả nguyên liệu BOM,
///             hàng stockable trả chính nó. <c>false</c> → không cộng (đồ đã nấu, không thu hồi).</item>
///         <item>Tiền đã được giảm khi dòng refund được gửi bếp (recompute) — bước này không đụng tiền.</item>
///     </list>
///     Quyền <c>order_item:process_return</c>.
/// </summary>
public static class ProcessReturnItem
{
    public sealed record Command(long OrderItemId, bool Restock, string? Note) : ICommand<Response>;

    public sealed record Response(long OrderItemId, string Status, bool Restocked);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderItemId).GreaterThan(0);
            RuleFor(x => x.Note).MaximumLength(500);
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IStockMovementService stockMovement,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            int? stationId = currentStaff.KitchenStationId;
            if (stationId is null)
                return Result.Failure<Response>(KitchenStationErrors.NotSelected);

            OrderItem? refund = await db.OrderItems
                .FirstOrDefaultAsync(oi => oi.Id == request.OrderItemId, ct);
            if (refund is null) return Result.Failure<Response>(OrderItemErrors.NotFound);

            // Phải là dòng trả hàng (âm + có OriginalOrderItemId).
            if (refund.Quantity >= 0 || refund.OriginalOrderItemId is null)
                return Result.Failure<Response>(OrderItemErrors.NotReturnLine);

            // Đúng khu bếp của phiên.
            if (refund.KitchenStationId != stationId.Value)
                return Result.Failure<Response>(OrderItemErrors.WrongStation);

            // Chỉ xử lý khi dòng trả hàng còn trong lifecycle (chưa terminal DONE/CANCELLED).
            if (refund.Status != OrderItemStatus.Pending
                && refund.Status != OrderItemStatus.Processing
                && refund.Status != OrderItemStatus.Ready)
                return Result.Failure<Response>(OrderItemErrors.ReturnNotActionable);

            string ticketStatus = await db.Tickets
                .Where(t => t.Id == refund.TicketId).Select(t => t.Status).FirstAsync(ct);
            if (ticketStatus != TicketStatus.Open)
                return Result.Failure<Response>(TicketErrors.NotOpen);

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            refund.Status = OrderItemStatus.Done;
            refund.DoneAt = now;
            refund.UpdatedAt = now;
            if (!string.IsNullOrWhiteSpace(request.Note))
                refund.CancellationNote = request.Note.Trim();

            // Roll-up: nếu tất cả món của ticket đã terminal → đóng các đợt liên quan.
            var allItems = await db.OrderItems
                .Where(oi => oi.TicketId == refund.TicketId)
                .Select(oi => new { oi.OrderId, oi.Status })
                .ToListAsync(ct);
            bool allTerminal = allItems.All(x =>
                x.Status == OrderItemStatus.Done || x.Status == OrderItemStatus.Cancelled);
            if (allTerminal)
            {
                var orderIds = allItems.Select(x => x.OrderId).Distinct().ToList();
                var orders = await db.Orders.Where(o => orderIds.Contains(o.Id)).ToListAsync(ct);
                foreach (var o in orders.Where(o => o.Status != OrderStatus.Done && o.Status != OrderStatus.Deleted))
                {
                    o.Status = OrderStatus.Done;
                    o.UpdatedAt = now;
                }
            }

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(OrderItem),
                EntityId = refund.Id,
                Action = "RETURN_PROCESS",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Bếp xử lý trả hàng món {refund.ItemCode} x{(-refund.Quantity)} | hoàn kho: {(request.Restock ? "CÓ" : "KHÔNG")}"
                          + (string.IsNullOrWhiteSpace(request.Note) ? "" : $" | {request.Note.Trim()}"),
            });

            await db.SaveChangesAsync(ct);

            // Hoàn kho (nếu bếp chọn) — idempotent trong service.
            if (request.Restock)
                await stockMovement.RestockReturnAsync(refund.Id, staffId, ct);

            await versionService.BumpAsync(VersionScopes.Kitchen, $"Return.Process(orderItemId={refund.Id})", ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Return.Process(orderItemId={refund.Id})", ct);

            return Result.Success(new Response(refund.Id, refund.Status, request.Restock));
        }
    }
}
