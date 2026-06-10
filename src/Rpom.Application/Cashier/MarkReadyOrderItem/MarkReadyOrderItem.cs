using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.MarkReadyOrderItem;

/// <summary>
///     Kitchen staff marks one or more order items as READY (dish cooked). PROCESSING → READY.
/// </summary>
public static class MarkReadyOrderItem
{
    public sealed record Command(long TicketId, IReadOnlyList<long> OrderItemIds) : ICommand<Response>;
    public sealed record Response(int UpdatedCount, string NewStatus);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);
            RuleFor(x => x.OrderItemIds).NotEmpty();
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        ITableOperationGuard guard,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var ticket = await db.Tickets
                .Where(t => t.Id == request.TicketId)
                .Select(t => new { t.Id, t.TableId, t.Status })
                .FirstOrDefaultAsync(ct);
            if (ticket is null) return Result.Failure<Response>(TicketErrors.NotFound);
            if (ticket.Status != TicketStatus.Open) return Result.Failure<Response>(TicketErrors.NotOpen);

            Result held = await guard.EnsureHeldAsync(ticket.TableId, currentStaff.StaffAccountId, ct);
            if (held.IsFailure) return Result.Failure<Response>(held.Error);

            var ids = request.OrderItemIds.Distinct().ToList();
            var items = await db.OrderItems
                .Where(oi => ids.Contains(oi.Id) && oi.TicketId == request.TicketId)
                .ToListAsync(ct);

            if (items.Count != ids.Count) return Result.Failure<Response>(OrderItemErrors.WrongTicket);
            if (items.Any(oi => oi.Status != OrderItemStatus.Processing))
                return Result.Failure<Response>(OrderItemErrors.NotProcessing);

            DateTime now = clock.UtcNow;
            foreach (var oi in items)
            {
                oi.Status = OrderItemStatus.Ready;
                oi.ReadyAt = now;
                oi.UpdatedAt = now;
            }

            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Kitchen, $"MarkReady(ticketId={ticket.Id})", ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"MarkReady(ticketId={ticket.Id})", ct);

            return Result.Success(new Response(items.Count, OrderItemStatus.Ready));
        }
    }
}
