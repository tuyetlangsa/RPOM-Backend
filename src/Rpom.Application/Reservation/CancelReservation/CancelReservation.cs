using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Reservation;
using Rpom.Domain.Sales;
using ReservationEntity = Rpom.Domain.Reservation.Reservation;

namespace Rpom.Application.Reservation.CancelReservation;

/// <summary>UC-R4. Cancels a still-BOOKED reservation (customer phoned to cancel). Reason required (BR-CR1).</summary>
public static class CancelReservation
{
    public sealed record Command(long ReservationId, int CancellationReasonId, string? Note) : ICommand;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ReservationId).GreaterThan(0L);
            RuleFor(x => x.CancellationReasonId).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            ReservationEntity? r = await db.Reservations
                .FirstOrDefaultAsync(x => x.Id == request.ReservationId, ct);
            if (r is null)
            {
                return Result.Failure(ReservationErrors.NotFound);
            }
            if (r.Status != ReservationStatus.Booked)
            {
                return Result.Failure(ReservationErrors.NotBooked);
            }

            bool reasonExists = await db.CancellationReasons
                .AnyAsync(x => x.Id == request.CancellationReasonId && x.IsActive, ct);
            if (!reasonExists)
            {
                return Result.Failure(CancellationReasonErrors.NotFound);
            }

            DateTime now = clock.UtcNow;
            r.Status = ReservationStatus.Cancelled;
            r.CancelledAt = now;
            r.CancellationReasonId = request.CancellationReasonId;
            r.CancellationNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            r.UpdatedAt = now;

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == currentStaff.StaffAccountId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "Reservation",
                EntityId = r.Id,
                Action = "CANCEL",
                ActorStaffAccountId = staff.Id,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Reservation {r.Code} cancelled."
            });
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Reservation.Cancel(id={r.Id})", ct);

            return Result.Success();
        }
    }
}
