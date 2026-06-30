using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;
using Rpom.Domain.Sales;

namespace Rpom.Application.Kitchen.PinKitchenOrder;

/// <summary>
///     Ghim 1 đợt (Order) lên đầu màn KDS của khu bếp hiện tại. Scope theo (Station, Order). Ghim lại
///     order đã ghim chỉ refresh PinnedAt → nổi lên trên các ghim cũ hơn. Idempotent. Permission <c>kds:view</c>.
/// </summary>
public static class PinKitchenOrder
{
    public sealed record Command(long OrderId) : ICommand;

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            int? stationId = currentStaff.KitchenStationId;
            if (stationId is null) return Result.Failure(KitchenStationErrors.NotSelected);

            bool orderExists = await db.Orders.AnyAsync(o => o.Id == request.OrderId, ct);
            if (!orderExists) return Result.Failure(OrderErrors.NotFound);

            DateTime now = clock.UtcNow;

            var pin = await db.KitchenOrderPins
                .FirstOrDefaultAsync(p => p.KitchenStationId == stationId.Value && p.OrderId == request.OrderId, ct);

            if (pin is null)
            {
                db.KitchenOrderPins.Add(new KitchenOrderPin
                {
                    KitchenStationId = stationId.Value,
                    OrderId = request.OrderId,
                    PinnedAt = now,
                    PinnedByStaffId = currentStaff.StaffAccountId
                });
            }
            else
            {
                // Re-pin → float above earlier pins.
                pin.PinnedAt = now;
                pin.PinnedByStaffId = currentStaff.StaffAccountId;
            }

            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Kitchen, $"PinKitchenOrder(station={stationId},order={request.OrderId})", ct);

            return Result.Success();
        }
    }
}
