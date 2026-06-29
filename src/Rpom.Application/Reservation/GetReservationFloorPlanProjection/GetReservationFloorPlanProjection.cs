using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Configuration;
using Rpom.Domain.Common;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Reservation.GetReservationFloorPlanProjection;

/// <summary>
///     UC-R5. Floor plan of a counter PROJECTED to a target time: each table is flagged
///     reserved-overlap iff a BOOKED reservation's window would overlap a new booking at
///     <see cref="Query.TargetTime" /> (BR-R1). Read-only.
/// </summary>
public static class GetReservationFloorPlanProjection
{
    public sealed record Query(int CounterId, DateTime TargetTime) : IQuery<Response>;

    public sealed record Response(
        int CounterId,
        DateTime TargetTime,
        IReadOnlyList<TableProjection> Tables,
        IReadOnlyList<ReservationBrief> OverlappingReservations);

    public sealed record TableProjection(
        int TableId, string TableCode, int AreaId, string AreaName, int SeatCount, bool IsReservedOverlap);

    public sealed record ReservationBrief(
        long ReservationId, string CustomerName, string CustomerPhone, short GuestCount,
        DateTime TargetTime, IReadOnlyList<int> TableIds);

    internal sealed class Handler(IDbContext db, IConfigValueService config)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            bool counterExists = await db.Counters.AnyAsync(c => c.Id == request.CounterId && c.IsActive, ct);
            if (!counterExists)
            {
                return Result.Failure<Response>(CounterErrors.NotFound);
            }

            int pre = await config.GetIntAsync(ConfigCodes.ReservationPreBufferMinutes, 30, ct);
            int grace = await config.GetIntAsync(ConfigCodes.ReservationGracePeriodMinutes, 30, ct);

            var tables = await db.Tables
                .Where(t => t.Area.CounterId == request.CounterId && t.IsActive)
                .OrderBy(t => t.AreaId).ThenBy(t => t.Code)
                .Select(t => new { t.Id, t.Code, t.AreaId, AreaName = t.Area.Name, t.SeatCount })
                .ToListAsync(ct);

            var booked = await db.Reservations
                .Where(r => r.CounterId == request.CounterId && r.Status == ReservationStatus.Booked)
                .Select(r => new
                {
                    r.Id, r.CustomerName, r.CustomerPhone, r.GuestCount, r.TargetTime,
                    TableIds = r.ReservationTables.Select(rt => rt.TableId).ToList()
                })
                .ToListAsync(ct);

            var overlapping = booked
                .Where(r => ReservationWindow.Overlaps(r.TargetTime, request.TargetTime, pre, grace))
                .ToList();
            var overlapTableIds = overlapping.SelectMany(r => r.TableIds).ToHashSet();

            var tableDtos = tables
                .Select(t => new TableProjection(
                    t.Id, t.Code, t.AreaId, t.AreaName, t.SeatCount, overlapTableIds.Contains(t.Id)))
                .ToList();

            var resBriefs = overlapping
                .Select(r => new ReservationBrief(
                    r.Id, r.CustomerName, r.CustomerPhone, r.GuestCount, r.TargetTime, r.TableIds))
                .ToList();

            return Result.Success(new Response(request.CounterId, request.TargetTime, tableDtos, resBriefs));
        }
    }
}
