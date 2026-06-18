using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Inventory;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;
using Rpom.Domain.Sales;

namespace Rpom.Application.Kitchen.StartCookOrderItem;

/// <summary>
///     Bếp bắt đầu chế biến một/nhiều món. PENDING → PROCESSING. Đợt SENT → PROCESSING.
///     <para>
///     Tại bước này **trừ kho** qua <see cref="IStockMovementService.DeductAsync"/>:
///     món công thức trừ nguyên liệu BOM, hàng stockable trừ chính nó.
///     </para>
///     Gate theo khu bếp (claim) — chỉ món thuộc station đang đăng nhập mới thao tác được.
///     Không dùng table-lock (bếp không giữ khoá bàn). Quyền <c>order_item:start_cooking</c>.
/// </summary>
public static class StartCookOrderItem
{
    public sealed record Command(IReadOnlyList<long> OrderItemIds) : ICommand<Response>;

    public sealed record Response(int UpdatedCount, string NewStatus);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderItemIds).NotEmpty();
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService,
        IStockMovementService stockMovement) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            int? stationId = currentStaff.KitchenStationId;
            if (stationId is null) return Result.Failure<Response>(KitchenStationErrors.NotSelected);

            var ids = request.OrderItemIds.Distinct().ToList();
            var items = await db.OrderItems.Where(oi => ids.Contains(oi.Id)).ToListAsync(ct);

            if (items.Count != ids.Count) return Result.Failure<Response>(OrderItemErrors.NotFound);
            if (items.Any(oi => oi.KitchenStationId != stationId.Value))
                return Result.Failure<Response>(OrderItemErrors.WrongStation);
            if (items.Any(oi => oi.Status != OrderItemStatus.Pending))
                return Result.Failure<Response>(OrderItemErrors.NotPending);

            var ticketIds = items.Select(oi => oi.TicketId).Distinct().ToList();
            var openTicketIds = (await db.Tickets
                .Where(t => ticketIds.Contains(t.Id) && t.Status == TicketStatus.Open)
                .Select(t => t.Id).ToListAsync(ct)).ToHashSet();
            if (items.Any(oi => !openTicketIds.Contains(oi.TicketId)))
                return Result.Failure<Response>(TicketErrors.NotOpen);

            DateTime now = clock.UtcNow;
            int staffId = currentStaff.StaffAccountId;

            foreach (var oi in items)
            {
                oi.Status = OrderItemStatus.Processing;
                oi.StartCookAt = now;
                oi.UpdatedAt = now;
            }

            // Đợt SENT → PROCESSING.
            var orderIds = items.Select(oi => oi.OrderId).Distinct().ToList();
            var orders = await db.Orders.Where(o => orderIds.Contains(o.Id)).ToListAsync(ct);
            foreach (var o in orders.Where(o => o.Status == OrderStatus.Sent))
            {
                o.Status = OrderStatus.Processing;
                o.UpdatedAt = now;
            }

            await db.SaveChangesAsync(ct);

            // Trừ kho cho từng món bắt đầu chế biến.
            foreach (var oi in items)
                await stockMovement.DeductAsync(oi.Id, staffId, ct);

            await versionService.BumpAsync(VersionScopes.Kitchen, $"StartCook(station={stationId})", ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"StartCook(station={stationId})", ct);

            return Result.Success(new Response(items.Count, OrderItemStatus.Processing));
        }
    }
}
