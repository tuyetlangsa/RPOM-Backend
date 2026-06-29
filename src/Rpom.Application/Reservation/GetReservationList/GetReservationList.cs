using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Configuration;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Reservation;
using ReservationEntity = Rpom.Domain.Reservation.Reservation;

namespace Rpom.Application.Reservation.GetReservationList;

/// <summary>
///     UC-R2. Counter-scoped, day-filtered, time-sorted list. Performs lazy-expire (BR-R8):
///     BOOKED reservations past window_end are flipped to NOT_ARRIVED on read.
/// </summary>
public static class GetReservationList
{
    public sealed record Query(int CounterId, DateOnly Date, string? Status) : IQuery<Response>;

    public sealed record Response(IReadOnlyList<Item> Items);

    public sealed record Item(
        long ReservationId,
        string Code,
        string CustomerName,
        string CustomerPhone,
        short GuestCount,
        DateTime TargetTime,
        string Status,
        string? Phase,
        IReadOnlyList<int> TableIds);

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IConfigValueService config) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            DateTime now = clock.UtcNow;
            int pre = await config.GetIntAsync(ConfigCodes.ReservationPreBufferMinutes, 30, ct);
            int grace = await config.GetIntAsync(ConfigCodes.ReservationGracePeriodMinutes, 30, ct);

            DateTime dayStart = request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            DateTime dayEnd = dayStart.AddDays(1);

            List<ReservationEntity> rows = await db.Reservations
                .Where(r => r.CounterId == request.CounterId
                            && r.TargetTime >= dayStart && r.TargetTime < dayEnd)
                .OrderBy(r => r.TargetTime)
                .ToListAsync(ct);

            // BR-R8 lazy no-show expiry: flip BOOKED → NOT_ARRIVED past the grace window.
            var expired = rows
                .Where(r => r.Status == ReservationStatus.Booked
                            && now > r.TargetTime.AddMinutes(grace))
                .ToList();

            if (expired.Count > 0)
            {
                StaffAccount actor = await db.StaffAccounts
                    .FirstAsync(s => s.Id == currentStaff.StaffAccountId, ct);

                foreach (ReservationEntity r in expired)
                {
                    r.Status = ReservationStatus.NotArrived;
                    r.UpdatedAt = now;
                    db.AuditLogs.Add(new AuditLog
                    {
                        EntityType = "Reservation",
                        EntityId = r.Id,
                        Action = "NOT_ARRIVED",
                        ActorStaffAccountId = actor.Id,
                        ActorFullName = actor.FullName,
                        Timestamp = now,
                        Summary = $"Reservation {r.Code} auto-expired (no-show) past window."
                    });
                }

                await db.SaveChangesAsync(ct);
            }

            // Load table assignments for all rows in one query.
            var rowIds = rows.Select(r => r.Id).ToList();
            var tableIdsByRes = await db.ReservationTables
                .Where(rt => rowIds.Contains(rt.ReservationId))
                .GroupBy(rt => rt.ReservationId)
                .Select(g => new { ReservationId = g.Key, Ids = g.Select(x => x.TableId).ToList() })
                .ToListAsync(ct);
            var idsMap = tableIdsByRes.ToDictionary(x => x.ReservationId, x => x.Ids);

            string? statusFilter = request.Status;
            var items = rows
                .Where(r => statusFilter is null || r.Status == statusFilter)
                .Select(r => new Item(
                    r.Id,
                    r.Code,
                    r.CustomerName,
                    r.CustomerPhone,
                    r.GuestCount,
                    r.TargetTime,
                    r.Status,
                    r.Status == ReservationStatus.Booked
                        ? ReservationWindow.Phase(r.TargetTime, pre, grace, now)
                        : null,
                    idsMap.GetValueOrDefault(r.Id) ?? new List<int>()))
                .ToList();

            return Result.Success(new Response(items));
        }
    }
}
