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

namespace Rpom.Application.Kitchen.MarkReadyOrderItem;

/// <summary>
///     Bếp báo món đã làm xong. PROCESSING → READY. Gate theo khu bếp (claim);
///     không dùng table-lock. Quyền <c>order_item:mark_ready</c>.
/// </summary>
public static class MarkReadyOrderItem
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
        IVersionService versionService) : ICommandHandler<Command, Response>
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
            if (items.Any(oi => oi.Status != OrderItemStatus.Processing))
                return Result.Failure<Response>(OrderItemErrors.NotProcessing);

            var ticketIds = items.Select(oi => oi.TicketId).Distinct().ToList();
            var openTicketIds = (await db.Tickets
                .Where(t => ticketIds.Contains(t.Id) && t.Status == TicketStatus.Open)
                .Select(t => t.Id).ToListAsync(ct)).ToHashSet();
            if (items.Any(oi => !openTicketIds.Contains(oi.TicketId)))
                return Result.Failure<Response>(TicketErrors.NotOpen);

            DateTime now = clock.UtcNow;
            foreach (var oi in items)
            {
                oi.Status = OrderItemStatus.Ready;
                oi.ReadyAt = now;
                oi.UpdatedAt = now;
            }

            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Kitchen, $"MarkReady(station={stationId})", ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"MarkReady(station={stationId})", ct);

            return Result.Success(new Response(items.Count, OrderItemStatus.Ready));
        }
    }
}
