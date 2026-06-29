using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Configuration;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Reservation;
using Rpom.Domain.Restaurant;
using ReservationEntity = Rpom.Domain.Reservation.Reservation;

namespace Rpom.Application.Reservation.CreateReservation;

/// <summary>
///     UC-R1. Books one or more tables (all in <paramref name="CounterId" />) for a future time.
///     Rejects cross-counter table sets (BR-R6) and per-table window overlaps (BR-R1).
/// </summary>
public static class CreateReservation
{
    public sealed record Command(
        int CounterId,
        DateTime TargetTime,
        string CustomerName,
        string CustomerPhone,
        short GuestCount,
        string? Note,
        IReadOnlyList<int> TableIds) : ICommand<Response>;

    public sealed record Response(long ReservationId, string Code);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.CounterId).GreaterThan(0);
            RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.CustomerPhone).NotEmpty().MaximumLength(20);
            RuleFor(x => x.GuestCount).GreaterThan((short)0);
            RuleFor(x => x.TableIds).NotEmpty();
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IConfigValueService config,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var tableIds = request.TableIds.Distinct().ToList();
            if (tableIds.Count == 0)
            {
                return Result.Failure<Response>(ReservationErrors.NoTables);
            }

            var tables = await db.Tables
                .Where(t => tableIds.Contains(t.Id) && t.IsActive)
                .Select(t => new { t.Id, t.Area.CounterId })
                .ToListAsync(ct);
            if (tables.Count != tableIds.Count)
            {
                return Result.Failure<Response>(TableErrors.NotFound);
            }
            if (tables.Any(t => t.CounterId != request.CounterId))
            {
                return Result.Failure<Response>(ReservationErrors.TablesCrossCounter);
            }

            int pre = await config.GetIntAsync(ConfigCodes.ReservationPreBufferMinutes, 30, ct);
            int grace = await config.GetIntAsync(ConfigCodes.ReservationGracePeriodMinutes, 30, ct);

            var existing = await db.ReservationTables
                .Where(rt => tableIds.Contains(rt.TableId)
                             && rt.Reservation.Status == ReservationStatus.Booked)
                .Select(rt => rt.Reservation.TargetTime)
                .ToListAsync(ct);
            if (existing.Any(t => ReservationWindow.Overlaps(t, request.TargetTime, pre, grace)))
            {
                return Result.Failure<Response>(ReservationErrors.TableOverlap);
            }

            DateTime now = clock.UtcNow;
            var reservation = new ReservationEntity
            {
                Code = "R-PENDING",
                CounterId = request.CounterId,
                CustomerName = request.CustomerName.Trim(),
                CustomerPhone = request.CustomerPhone.Trim(),
                GuestCount = request.GuestCount,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                TargetTime = request.TargetTime,
                Status = ReservationStatus.Booked,
                CreatedByStaffId = currentStaff.StaffAccountId,
                CreatedAt = now,
                UpdatedAt = now,
                ReservationTables = tableIds
                    .Select(id => new ReservationTable { TableId = id, CreatedAt = now })
                    .ToList()
            };
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(ct);

            reservation.Code = $"R-{now:yyyy}-{reservation.Id}";

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == currentStaff.StaffAccountId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Reservation",
                EntityId = reservation.Id,
                Action = "CREATE",
                ActorStaffAccountId = staff.Id,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Reservation created: {reservation.Code} on {tableIds.Count} table(s) @ {request.TargetTime:u}"
            });
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Reservation.Create(id={reservation.Id})", ct);

            return Result.Success(new Response(reservation.Id, reservation.Code));
        }
    }
}
