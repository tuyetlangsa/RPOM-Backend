using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Configuration;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.TransferTable;

/// <summary>
///     Move an OPEN ticket to another table at the SAME counter. SENT order items keep their
///     snapshot prices. When the target table is in a different area: the DRAFT cart is cleared
///     (menu/price differ) and — per config <see cref="ConfigCodes.TransferUseTargetAreaServiceCharge" />
///     — the service-charge percents are re-snapshotted from the target area and the ticket is
///     recomputed. Requires the caller to hold the source table's operation lock.
/// </summary>
public static class TransferTable
{
    public sealed record Command(long TicketId, int TargetTableId) : ICommand<Response>;

    public sealed record Response(
        long TicketId,
        int TableId,
        int AreaId,
        decimal ServiceChargePercent,
        decimal TotalAmount,
        int ClearedCartItemCount);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);
            RuleFor(x => x.TargetTableId).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        ITableOperationGuard guard,
        ITicketRecomputeService ticketRecompute,
        IConfigValueService config,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            int staffId = currentStaff.StaffAccountId;

            Ticket? ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct);
            if (ticket is null)
            {
                return Result.Failure<Response>(TicketErrors.NotFound);
            }

            if (ticket.Status != TicketStatus.Open)
            {
                return Result.Failure<Response>(TicketErrors.NotOpen);
            }

            Result held = await guard.EnsureHeldAsync(ticket.TableId, staffId, ct);
            if (held.IsFailure)
            {
                return Result.Failure<Response>(held.Error);
            }

            var target = await db.Tables
                .Where(t => t.Id == request.TargetTableId && t.IsActive)
                .Select(t => new
                {
                    t.Id,
                    t.AreaId,
                    t.Area.CounterId,
                    t.Area.ServiceChargePercent,
                    t.Area.ServiceChargeVatPercent
                })
                .FirstOrDefaultAsync(ct);
            if (target is null)
            {
                return Result.Failure<Response>(TableErrors.NotFound);
            }

            if (target.Id == ticket.TableId)
            {
                return Result.Failure<Response>(TicketErrors.TransferSameTable);
            }

            if (target.CounterId != ticket.CounterId)
            {
                return Result.Failure<Response>(TicketErrors.TransferCrossCounter);
            }

            DateTime now = clock.UtcNow;
            int oldTableId = ticket.TableId;
            int oldAreaId = ticket.AreaId;
            bool areaChanged = target.AreaId != oldAreaId;

            ticket.TableId = target.Id;
            ticket.AreaId = target.AreaId;

            int clearedCartItemCount = 0;
            bool useTargetSc = true;

            if (areaChanged)
            {
                // Drop the DRAFT cart — the target area's menu/prices differ.
                List<long> draftOrderIds = await db.Orders
                    .Where(o => o.TicketId == ticket.Id && o.Status == OrderStatus.Draft)
                    .Select(o => o.Id)
                    .ToListAsync(ct);
                if (draftOrderIds.Count > 0)
                {
                    List<CartItem> cartItems = await db.CartItems
                        .Where(c => draftOrderIds.Contains(c.OrderId))
                        .ToListAsync(ct);
                    clearedCartItemCount = cartItems.Count;
                    db.CartItems.RemoveRange(cartItems); // CartItemDetail cascades
                }

                useTargetSc = await config.GetBoolAsync(
                    ConfigCodes.TransferUseTargetAreaServiceCharge, true, ct);
                if (useTargetSc)
                {
                    ticket.ServiceChargePercent = target.ServiceChargePercent;
                    ticket.ServiceChargeVatPercent = target.ServiceChargeVatPercent;
                    await ticketRecompute.RecomputeAsync(ticket.Id, ct);
                }
            }

            Table targetRow = await db.Tables.FirstAsync(t => t.Id == target.Id, ct);
            targetRow.Status = TableStatus.Occupied;
            targetRow.UpdatedAt = now;

            ticket.UpdatedAt = now;

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);
            string scMode = areaChanged ? (useTargetSc ? "target-area" : "kept") : "unchanged";
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = ticket.Id,
                Action = "TRANSFER",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Transfer table {oldTableId} → {target.Id}; SC={scMode}; clearedCart={clearedCartItemCount}"
            });

            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Ticket.Transfer(id={ticket.Id})", ct);

            return Result.Success(new Response(
                ticket.Id, ticket.TableId, ticket.AreaId,
                ticket.ServiceChargePercent, ticket.TotalAmount, clearedCartItemCount));
        }
    }
}
