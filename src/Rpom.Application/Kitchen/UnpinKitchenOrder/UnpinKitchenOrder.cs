using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.Kitchen.UnpinKitchenOrder;

/// <summary>
///     Bỏ ghim 1 đợt (Order) khỏi màn KDS của khu bếp hiện tại. Scope theo (Station, Order). Idempotent —
///     không có dòng ghim thì trả về thành công. Permission <c>kds:view</c>.
/// </summary>
public static class UnpinKitchenOrder
{
    public sealed record Command(long OrderId) : ICommand;

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IVersionService versionService) : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            int? stationId = currentStaff.KitchenStationId;
            if (stationId is null) return Result.Failure(KitchenStationErrors.NotSelected);

            var pin = await db.KitchenOrderPins
                .FirstOrDefaultAsync(p => p.KitchenStationId == stationId.Value && p.OrderId == request.OrderId, ct);

            if (pin is null) return Result.Success();

            db.KitchenOrderPins.Remove(pin);
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Kitchen, $"UnpinKitchenOrder(station={stationId},order={request.OrderId})", ct);

            return Result.Success();
        }
    }
}
