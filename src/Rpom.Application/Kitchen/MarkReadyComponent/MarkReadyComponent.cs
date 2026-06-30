using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;
using Rpom.Domain.Sales;

namespace Rpom.Application.Kitchen.MarkReadyComponent;
public static class MarkReadyComponent
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
        IVersionService versionService) : ICommandHandler<Command, Response>
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
            if (comps.Any(d => d.Status != OrderItemStatus.Processing))
                return Result.Failure<Response>(OrderItemDetailErrors.NotProcessing);

            var parentIds = comps.Select(d => d.OrderItemId).Distinct().ToList();
            var ticketIds = await db.OrderItems
                .Where(o => parentIds.Contains(o.Id)).Select(o => o.TicketId).Distinct().ToListAsync(ct);
            var openTicketIds = (await db.Tickets
                .Where(t => ticketIds.Contains(t.Id) && t.Status == TicketStatus.Open)
                .Select(t => t.Id).ToListAsync(ct)).ToHashSet();
            if (ticketIds.Any(tid => !openTicketIds.Contains(tid)))
                return Result.Failure<Response>(TicketErrors.NotOpen);

            DateTime now = clock.UtcNow;
            foreach (var d in comps)
            {
                d.Status = OrderItemStatus.Ready;
                d.ReadyAt = now;
                d.UpdatedAt = now;
            }

            await db.SaveChangesAsync(ct);

            await SetComponentRollup.RecomputeAsync(db, parentIds, now, ct);
            await db.SaveChangesAsync(ct);

            await versionService.BumpAsync(VersionScopes.Kitchen, $"MarkReadyComponent(station={stationId})", ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"MarkReadyComponent(station={stationId})", ct);

            return Result.Success(new Response(comps.Count, OrderItemStatus.Ready));
        }
    }
}
