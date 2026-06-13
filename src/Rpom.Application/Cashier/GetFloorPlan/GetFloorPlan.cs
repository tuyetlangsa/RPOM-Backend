using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Configuration;
using Rpom.Domain.Common;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.GetFloorPlan;

public static class GetFloorPlan
{
    public sealed record Query(int CounterId) : IQuery<Response>;

    public sealed record Response(
        int CounterId,
        string CounterName,
        DateTime ServerTime,
        IReadOnlyList<AreaDto> Areas);

    public sealed record AreaDto(
        int AreaId,
        string AreaName,
        short DisplayOrder,
        decimal ServiceChargePercent,
        IReadOnlyList<TableDto> Tables);

    public sealed record TableDto(
        int TableId,
        string TableCode,
        int SeatCount,
        string Status, // AVAILABLE | OCCUPIED
        int OpenTicketCount,
        TicketBrief? LatestTicket,
        ReservationBrief? UpcomingReservation,
        int? LockedByStaffId, // null when free or stale
        string? LockedByName,
        DateTime? LockedSince);

    public sealed record TicketBrief(
        long TicketId,
        string TicketCode,
        short GuestCount,
        string? WaiterName,
        DateTime OpenedAt,
        int DurationMinutes,
        decimal Subtotal,
        decimal TotalAmount,
        bool HasUnsentItems);

    public sealed record ReservationBrief(
        long ReservationId,
        string CustomerName,
        string CustomerPhone,
        short GuestCount,
        DateTime TargetTime,
        string Status);

    internal sealed class Handler(
        IDbContext db,
        IDateTimeProvider clock,
        IConfigValueService config) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var counter = await db.Counters
                .Where(c => c.Id == request.CounterId && c.IsActive)
                .Select(c => new { c.Id, c.Name })
                .FirstOrDefaultAsync(ct);
            if (counter is null)
            {
                return Result.Failure<Response>(CounterErrors.NotFound);
            }

            DateTime now = clock.UtcNow;
            int preBuffer = await config.GetIntAsync(
                ConfigCodes.ReservationPreBufferMinutes, 30, ct);
            int grace = await config.GetIntAsync(
                ConfigCodes.ReservationGracePeriodMinutes, 30, ct);
            int lockTtl = await config.GetIntAsync(
                ConfigCodes.TableLockTtlSeconds, ITableOperationGuard.DefaultTtlSeconds, ct);

            var areas = await db.Areas
                .Where(a => a.CounterId == counter.Id && a.IsActive)
                .OrderBy(a => a.DisplayOrder)
                .Select(a => new { a.Id, a.Name, a.DisplayOrder, a.ServiceChargePercent })
                .ToListAsync(ct);
            var areaIds = areas.Select(a => a.Id).ToList();

            var tables = await db.Tables
                .Where(t => areaIds.Contains(t.AreaId) && t.IsActive)
                .OrderBy(t => t.AreaId).ThenBy(t => t.Code)
                .Select(t => new { t.Id, t.AreaId, t.Code, t.SeatCount })
                .ToListAsync(ct);
            var tableIds = tables.Select(t => t.Id).ToList();

            // All OPEN tickets on these tables, newest first.
            var openTickets = await db.Tickets
                .Where(tk => tableIds.Contains(tk.TableId) && tk.Status == TicketStatus.Open)
                .OrderByDescending(tk => tk.OpenedAt)
                .Select(tk => new
                {
                    tk.Id,
                    tk.Code,
                    tk.TableId,
                    tk.GuestCount,
                    tk.OpenedAt,
                    tk.Subtotal,
                    tk.TotalAmount,
                    WaiterName = tk.WaiterStaff != null ? tk.WaiterStaff.FullName : null
                })
                .ToListAsync(ct);
            var openTicketIds = openTickets.Select(t => t.Id).ToList();

            // HasUnsentItems flag computed in-memory: set of ticket-ids that have a DRAFT
            // order containing >= 1 CartItem. Avoids a nested .Any() inside the projection
            // which is risky for EF translation.
            List<long> ticketsWithUnsentItems = await db.Orders
                .Where(o => openTicketIds.Contains(o.TicketId)
                            && o.Status == OrderStatus.Draft
                            && db.CartItems.Any(ci => ci.OrderId == o.Id))
                .Select(o => o.TicketId)
                .Distinct()
                .ToListAsync(ct);
            var unsentTicketIds = ticketsWithUnsentItems.ToHashSet();

            // Upcoming reservations for these tables.
            var reservations = await db.Reservations
                .Where(r => tableIds.Contains(r.TableId) && r.Status == ReservationStatus.Booked)
                .Select(r => new
                {
                    r.Id,
                    r.TableId,
                    r.CustomerName,
                    r.CustomerPhone,
                    r.GuestCount,
                    r.TargetTime,
                    r.Status
                })
                .ToListAsync(ct);

            // Live operation locks on these tables (stale ones filtered in memory).
            DateTime lockCutoff = now.AddSeconds(-lockTtl);
            var liveLocks = await db.TableLocks
                .Where(l => tableIds.Contains(l.TableId) && l.LastHeartbeatAt >= lockCutoff)
                .Select(l => new { l.TableId, l.StaffAccountId, l.StaffName, l.AcquiredAt })
                .ToListAsync(ct);
            var lockByTable = liveLocks.ToDictionary(l => l.TableId);

            var ticketsByTable = openTickets.GroupBy(t => t.TableId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var areaDtos = areas.Select(a => new AreaDto(
                    a.Id, a.Name, a.DisplayOrder, a.ServiceChargePercent,
                    tables.Where(t => t.AreaId == a.Id).Select(t =>
                    {
                        var ts = ticketsByTable.GetValueOrDefault(t.Id) ?? new();
                        var latest = ts.FirstOrDefault();
                        var upcoming = reservations
                            .Where(r => r.TableId == t.Id
                                        && r.TargetTime.AddMinutes(-preBuffer) <= now
                                        && now <= r.TargetTime.AddMinutes(grace))
                            .OrderBy(r => r.TargetTime)
                            .FirstOrDefault();

                        var lk = lockByTable.GetValueOrDefault(t.Id);

                        return new TableDto(
                            t.Id, t.Code, t.SeatCount,
                            ts.Count > 0 ? "OCCUPIED" : "AVAILABLE",
                            ts.Count,
                            latest is null
                                ? null
                                : new TicketBrief(
                                    latest.Id, latest.Code, latest.GuestCount, latest.WaiterName,
                                    latest.OpenedAt, (int)(now - latest.OpenedAt).TotalMinutes,
                                    latest.Subtotal, latest.TotalAmount,
                                    unsentTicketIds.Contains(latest.Id)),
                            upcoming is null
                                ? null
                                : new ReservationBrief(
                                    upcoming.Id, upcoming.CustomerName, upcoming.CustomerPhone,
                                    upcoming.GuestCount, upcoming.TargetTime, upcoming.Status),
                            lk?.StaffAccountId, lk?.StaffName, lk?.AcquiredAt);
                    }).ToList()))
                .ToList();

            return Result.Success(new Response(counter.Id, counter.Name, now, areaDtos));
        }
    }
}
