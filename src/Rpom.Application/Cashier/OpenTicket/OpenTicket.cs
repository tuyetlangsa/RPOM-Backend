using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;

namespace Rpom.Application.Cashier.OpenTicket;

/// <summary>
///     Open a new ticket on a table. counterId/areaId are derived from the table (authoritative);
///     shiftId comes from the request (client chose counter+shift at login). Snapshots the area's
///     service-charge percents, marks the table OCCUPIED, and bumps FLOOR_PLAN. Requires the caller
///     to hold the table operation lock. Multiple OPEN tickets per table are allowed (split bills).
/// </summary>
public static class OpenTicket
{
    public sealed record Command(int TableId, short GuestCount, int ShiftId, string? Notes)
        : ICommand<Response>;

    public sealed record Response(long TicketId, string Code);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TableId).GreaterThan(0);
            RuleFor(x => x.ShiftId).GreaterThan(0);
            RuleFor(x => x.GuestCount).GreaterThan((short)0);
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
            int staffId = currentStaff.StaffAccountId;

            Result held = await guard.EnsureHeldAsync(request.TableId, staffId, ct);
            if (held.IsFailure)
            {
                return Result.Failure<Response>(held.Error);
            }

            var table = await db.Tables
                .Where(t => t.Id == request.TableId && t.IsActive)
                .Select(t => new
                {
                    t.Id, t.AreaId, t.Area.CounterId,
                    t.Area.ServiceChargePercent, t.Area.ServiceChargeVatPercent
                })
                .FirstOrDefaultAsync(ct);
            if (table is null)
            {
                return Result.Failure<Response>(TableErrors.NotFound);
            }

            var drawer = await db.CashDrawerSessions
                .Where(d => d.CounterId == table.CounterId && d.Status == CashDrawerStatus.Open)
                .Select(d => new { d.Id })
                .FirstOrDefaultAsync(ct);
            if (drawer is null)
            {
                return Result.Failure<Response>(TicketErrors.NoOpenCashDrawer);
            }

            bool shiftExists = await db.Shifts.AnyAsync(s => s.Id == request.ShiftId, ct);
            if (!shiftExists)
            {
                return Result.Failure<Response>(TicketErrors.ShiftNotFound);
            }

            DateTime now = clock.UtcNow;
            var ticket = new Ticket
            {
                Code = "TK-PENDING", // replaced with TK-{date}-{id} after first save
                TableId = table.Id,
                AreaId = table.AreaId,
                CounterId = table.CounterId,
                CashDrawerSessionId = drawer.Id,
                ShiftId = request.ShiftId,
                GuestCount = request.GuestCount,
                WaiterStaffId = staffId,
                Status = TicketStatus.Open,
                OpenedAt = now,
                ServiceChargePercent = table.ServiceChargePercent,
                ServiceChargeVatPercent = table.ServiceChargeVatPercent,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync(ct);

            ticket.Code = $"TK-{now:yyyyMMdd}-{ticket.Id}";

            Table tableRow = await db.Tables.FirstAsync(t => t.Id == request.TableId, ct);
            tableRow.Status = TableStatus.Occupied;
            tableRow.UpdatedAt = now;

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = ticket.Id,
                Action = "OPEN",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Ticket opened: {ticket.Code} on table {request.TableId}"
            });
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Ticket.Open(id={ticket.Id})", ct);

            return Result.Success(new Response(ticket.Id, ticket.Code));
        }
    }
}
