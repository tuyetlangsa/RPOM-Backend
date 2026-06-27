using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Configuration;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;
using ReservationEntity = Rpom.Domain.Reservation.Reservation;

namespace Rpom.Application.Reservation.SeatReservation;

/// <summary>
///     UC-R3. Opens one independent ticket per selected table (actual seated tables, which may
///     differ from the booked tables), links them via Ticket.ReservationId, marks the reservation
///     ARRIVED. Requires each table's operation lock. Rejects past-window (BR-R7) and cross-counter.
/// </summary>
public static class SeatReservation
{
    public sealed record SeatTable(int TableId, short GuestCount);

    public sealed record Command(long ReservationId, IReadOnlyList<SeatTable> Tables) : ICommand<Response>;

    public sealed record Response(IReadOnlyList<OpenedTicket> Tickets);

    public sealed record OpenedTicket(long TicketId, string Code, int TableId);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ReservationId).GreaterThan(0L);
            RuleFor(x => x.Tables).NotEmpty();
            RuleForEach(x => x.Tables).ChildRules(t =>
            {
                t.RuleFor(y => y.TableId).GreaterThan(0);
                t.RuleFor(y => y.GuestCount).GreaterThan((short)0);
            });
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        ITableOperationGuard guard,
        IConfigValueService config,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            int staffId = currentStaff.StaffAccountId;
            var seatTables = request.Tables
                .GroupBy(t => t.TableId)
                .Select(g => new SeatTable(g.Key, g.First().GuestCount))
                .ToList();

            ReservationEntity? reservation = await db.Reservations
                .FirstOrDefaultAsync(r => r.Id == request.ReservationId, ct);
            if (reservation is null)
            {
                return Result.Failure<Response>(ReservationErrors.NotFound);
            }
            if (reservation.Status != ReservationStatus.Booked)
            {
                return Result.Failure<Response>(ReservationErrors.NotBooked);
            }

            int grace = await config.GetIntAsync(ConfigCodes.ReservationGracePeriodMinutes, 30, ct);
            DateTime now = clock.UtcNow;
            if (now > reservation.TargetTime.AddMinutes(grace))
            {
                return Result.Failure<Response>(ReservationErrors.WindowExpired);
            }

            foreach (SeatTable st in seatTables)
            {
                Result held = await guard.EnsureHeldAsync(st.TableId, staffId, ct);
                if (held.IsFailure)
                {
                    return Result.Failure<Response>(held.Error);
                }
            }

            var tableIds = seatTables.Select(s => s.TableId).ToList();
            var tables = await db.Tables
                .Where(t => tableIds.Contains(t.Id) && t.IsActive)
                .Select(t => new
                {
                    t.Id,
                    t.AreaId,
                    t.Area.CounterId,
                    t.Area.ServiceChargePercent,
                    t.Area.ServiceChargeVatPercent
                })
                .ToListAsync(ct);
            if (tables.Count != seatTables.Count)
            {
                return Result.Failure<Response>(TableErrors.NotFound);
            }
            if (tables.Any(t => t.CounterId != reservation.CounterId))
            {
                return Result.Failure<Response>(ReservationErrors.SeatTablesCrossCounter);
            }

            var drawer = await db.CashDrawerSessions
                .Where(d => d.CounterId == reservation.CounterId && d.Status == CashDrawerStatus.Open)
                .Select(d => new { d.Id, d.ShiftId })
                .FirstOrDefaultAsync(ct);
            if (drawer is null)
            {
                return Result.Failure<Response>(TicketErrors.NoOpenCashDrawer);
            }

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);
            var opened = new List<Ticket>();

            // Insert each ticket individually so it gets its DB-assigned ID before
            // we compute the final code (mirrors OpenTicket pattern; avoids unique-code
            // constraint violation when inserting multiple tickets in one batch).
            foreach (SeatTable st in seatTables)
            {
                var tbl = tables.First(t => t.Id == st.TableId);
                var ticket = new Ticket
                {
                    Code = "TK-PENDING",
                    TableId = tbl.Id,
                    AreaId = tbl.AreaId,
                    CounterId = tbl.CounterId,
                    CashDrawerSessionId = drawer.Id,
                    ShiftId = drawer.ShiftId,
                    GuestCount = st.GuestCount,
                    WaiterStaffId = staffId,
                    Status = TicketStatus.Open,
                    OpenedAt = now,
                    ServiceChargePercent = tbl.ServiceChargePercent,
                    ServiceChargeVatPercent = tbl.ServiceChargeVatPercent,
                    ReservationId = reservation.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Tickets.Add(ticket);
                await db.SaveChangesAsync(ct); // flush now to receive the DB-generated Id
                ticket.Code = $"TK-{now:yyyyMMdd}-{ticket.Id}";
                opened.Add(ticket);
            }

            foreach (Ticket ticket in opened)
            {
                Table tableRow = await db.Tables.FirstAsync(t => t.Id == ticket.TableId, ct);
                tableRow.Status = TableStatus.Occupied;
                tableRow.UpdatedAt = now;
                db.AuditLogs.Add(new AuditLog
                {
                    EntityType = nameof(Ticket),
                    EntityId = ticket.Id,
                    Action = "OPEN",
                    ActorStaffAccountId = staffId,
                    ActorFullName = staff.FullName,
                    Timestamp = now,
                    Summary = $"Ticket {ticket.Code} opened by seating reservation {reservation.Code}"
                });
            }

            reservation.Status = ReservationStatus.Arrived;
            reservation.ArrivedAt = now;
            reservation.UpdatedAt = now;
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Reservation",
                EntityId = reservation.Id,
                Action = "SEAT",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Reservation {reservation.Code} seated on {opened.Count} table(s)."
            });
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Reservation.Seat(id={reservation.Id})", ct);

            return Result.Success(new Response(
                opened.Select(t => new OpenedTicket(t.Id, t.Code, t.TableId)).ToList()));
        }
    }
}
