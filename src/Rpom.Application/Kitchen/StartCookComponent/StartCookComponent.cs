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

namespace Rpom.Application.Kitchen.StartCookComponent;

/// <summary>
///     The kitchen begins processing the INGREDIENTS of the set menu (OrderItemDetail) belonging to the kitchen area of ​​the kitchen station
///     PENDING → PROCESSING + subtract inventory for each component. Order SENT → PROCESSING; parent set
///     OrderItem status is re-inferred.
/// </summary>
public static class StartCookComponent
{
    public sealed record Command(IReadOnlyList<int> OrderItemDetailIds) : ICommand<Response>;

    public sealed record Response(int UpdatedCount, string NewStatus);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.OrderItemDetailIds).NotEmpty();
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

            var ids = request.OrderItemDetailIds.Distinct().ToList();
            var comps = await db.OrderItemDetails.Where(d => ids.Contains(d.Id)).ToListAsync(ct);

            if (comps.Count != ids.Count) return Result.Failure<Response>(OrderItemDetailErrors.NotFound);
            if (comps.Any(d => d.KitchenStationId != stationId.Value))
                return Result.Failure<Response>(OrderItemDetailErrors.WrongStation);
            if (comps.Any(d => d.Status != OrderItemStatus.Pending))
                return Result.Failure<Response>(OrderItemDetailErrors.NotPending);

            var parentIds = comps.Select(d => d.OrderItemId).Distinct().ToList();
            var parents = await db.OrderItems.Where(o => parentIds.Contains(o.Id)).ToListAsync(ct);

            var ticketIds = parents.Select(o => o.TicketId).Distinct().ToList();
            var openTicketIds = (await db.Tickets
                .Where(t => ticketIds.Contains(t.Id) && t.Status == TicketStatus.Open)
                .Select(t => t.Id).ToListAsync(ct)).ToHashSet();
            if (parents.Any(o => !openTicketIds.Contains(o.TicketId)))
                return Result.Failure<Response>(TicketErrors.NotOpen);

            DateTime now = clock.UtcNow;
            int staffId = currentStaff.StaffAccountId;

            foreach (var d in comps)
            {
                d.Status = OrderItemStatus.Processing;
                d.StartCookAt = now;
                d.UpdatedAt = now;
            }

            // Order SENT → PROCESSING.
            var orderIds = parents.Select(o => o.OrderId).Distinct().ToList();
            var orders = await db.Orders.Where(o => orderIds.Contains(o.Id)).ToListAsync(ct);
            foreach (var o in orders.Where(o => o.Status == OrderStatus.Sent))
            {
                o.Status = OrderStatus.Processing;
                o.UpdatedAt = now;
            }

            await db.SaveChangesAsync(ct);

            foreach (var d in comps)
                await stockMovement.DeductComponentAsync(d.Id, staffId, ct);

            await SetComponentRollup.RecomputeAsync(db, parentIds, now, ct);
            await db.SaveChangesAsync(ct);

            await versionService.BumpAsync(VersionScopes.Kitchen, $"StartCookComponent(station={stationId})", ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"StartCookComponent(station={stationId})", ct);

            return Result.Success(new Response(comps.Count, OrderItemStatus.Processing));
        }
    }
}
