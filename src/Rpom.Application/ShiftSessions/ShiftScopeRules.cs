using Rpom.Application.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.ShiftSessions;

/// <summary>
/// Role → scope mapping enforced at OpenShiftSession time.
/// <list type="bullet">
/// <item><c>CASHIER</c>: must pick Counter + hasCashTracking=true.</item>
/// <item><c>KITCHEN_STAFF</c>: must pick KitchenStation; hasCashTracking=false.</item>
/// <item><c>OWNER / MANAGER / ORDER_STAFF / ADMIN_VENDOR</c>: must pick Counter; hasCashTracking=false.</item>
/// </list>
/// </summary>
internal static class ShiftScopeRules
{
    public static Result Validate(string roleCode, int? counterId, int? kitchenStationId, bool hasCashTracking)
    {
        if (roleCode == Roles.Cashier)
        {
            if (counterId is null)
                return Result.Failure(ShiftSessionErrors.ScopeRequiredCounter);
            if (kitchenStationId is not null)
                return Result.Failure(ShiftSessionErrors.ScopeRequiredCounter);
            if (!hasCashTracking)
                return Result.Failure(ShiftSessionErrors.CashierMustTrackCash);
            return Result.Success();
        }

        if (roleCode == Roles.KitchenStaff)
        {
            if (kitchenStationId is null)
                return Result.Failure(ShiftSessionErrors.ScopeRequiredKitchenStation);
            if (counterId is not null)
                return Result.Failure(ShiftSessionErrors.ScopeRequiredKitchenStation);
            if (hasCashTracking)
                return Result.Failure(ShiftSessionErrors.NonCashierCannotTrackCash);
            return Result.Success();
        }

        // Owner, Manager, OrderStaff, AdminVendor → Counter, no cash tracking.
        if (counterId is null)
            return Result.Failure(ShiftSessionErrors.ScopeRequiredCounter);
        if (kitchenStationId is not null)
            return Result.Failure(ShiftSessionErrors.ScopeRequiredCounter);
        if (hasCashTracking)
            return Result.Failure(ShiftSessionErrors.NonCashierCannotTrackCash);
        return Result.Success();
    }
}
