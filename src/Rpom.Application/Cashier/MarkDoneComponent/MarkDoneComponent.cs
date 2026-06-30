using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Kitchen;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.MarkDoneComponent;
public static class MarkDoneComponent
{
    public sealed record Command(long TicketId, IReadOnlyList<int> OrderItemDetailIds) : ICommand<Response>;

    public sealed record Response(int UpdatedCount, string NewStatus);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);
            RuleFor(x => x.OrderItemDetailIds).NotEmpty();
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

            var ids = request.OrderItemDetailIds.Distinct().ToList();
            var comps = await db.OrderItemDetails
                .Where(d => ids.Contains(d.Id))
                .ToListAsync(ct);
            if (comps.Count != ids.Count) return Result.Failure<Response>(OrderItemDetailErrors.NotFound);

            var parentIds = comps.Select(d => d.OrderItemId).Distinct().ToList();
            var parents = await db.OrderItems
                .Where(o => parentIds.Contains(o.Id))
                .Select(o => new { o.Id, o.OrderId, o.TicketId })
                .ToListAsync(ct);
            if (parents.Any(p => p.TicketId != request.TicketId))
                return Result.Failure<Response>(OrderItemDetailErrors.WrongTicket);
            if (comps.Any(d => d.Status != OrderItemStatus.Ready))
                return Result.Failure<Response>(OrderItemDetailErrors.NotReady);

            DateTime now = clock.UtcNow;
            foreach (var d in comps)
            {
                d.Status = OrderItemStatus.Done;
                d.DoneAt = now;
                d.UpdatedAt = now;
            }
            await db.SaveChangesAsync(ct);

            // Parent set DONE when all component DONE.
            await SetComponentRollup.RecomputeAsync(db, parentIds, now, ct);
            await db.SaveChangesAsync(ct);

            // Order DONE khi all order item all.
            await OrderRollup.BumpFullyTerminalOrdersAsync(
                db, parents.Select(p => p.OrderId).Distinct().ToList(), now, ct);
            await db.SaveChangesAsync(ct);

            await versionService.BumpAsync(VersionScopes.Kitchen, $"MarkDoneComponent(ticketId={ticket.Id})", ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"MarkDoneComponent(ticketId={ticket.Id})", ct);

            return Result.Success(new Response(comps.Count, OrderItemStatus.Done));
        }
    }
}
