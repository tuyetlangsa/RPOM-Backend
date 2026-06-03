using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.ShiftSessions.Shared;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.ShiftSessions.CloseShiftSession;

public static class CloseShiftSession
{
    public sealed record CashCountInput(int DenominationId, int Quantity);

    public sealed record Command(
        long ShiftSessionId,
        IReadOnlyList<CashCountInput>? ClosingCashCounts) : ICommand<Response>;

    /// <summary>
    /// Response intentionally has NO access token — after close, FE must clear
    /// the JWT and redirect to login. Đóng ca = hết giờ làm; staff login lại để
    /// mở ca mới (or another staff takes over).
    /// </summary>
    public sealed record Response(ShiftSessionSummary ShiftSession);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ShiftSessionId).GreaterThan(0);
            RuleFor(x => x.ClosingCashCounts)
                .Must(list => list is null || list.All(c => c.Quantity >= 0))
                .WithMessage("Quantity của mỗi cash count phải ≥ 0.");
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var staffId = currentStaff.StaffAccountId;

            // 1. Load session
            var session = await dbContext.ShiftSessions
                .Include(x => x.CashCounts)
                .FirstOrDefaultAsync(x => x.Id == request.ShiftSessionId, ct);

            if (session is null) return Result.Failure<Response>(ShiftSessionErrors.NotFound);

            // 2. Ownership + state checks
            if (session.StaffAccountId != staffId)
                return Result.Failure<Response>(ShiftSessionErrors.NotOwner);
            if (session.Status != ShiftSessionStatus.Open)
                return Result.Failure<Response>(ShiftSessionErrors.NotOpen);

            // 3. Cash count seeding (cashier only)
            if (session.HasCashTracking)
            {
                if (request.ClosingCashCounts is null || request.ClosingCashCounts.Count == 0)
                    return Result.Failure<Response>(ShiftSessionErrors.CashCountsRequired);

                var requestedIds = request.ClosingCashCounts.Select(x => x.DenominationId).Distinct().ToList();
                var denoms = await dbContext.Denominations
                    .Where(x => requestedIds.Contains(x.Id) && x.IsActive)
                    .ToListAsync(ct);
                if (denoms.Count != requestedIds.Count)
                    return Result.Failure<Response>(ShiftSessionErrors.DenominationInvalid);

                var denomFaceById = denoms.ToDictionary(d => d.Id, d => d.FaceValue);
                var now = clock.UtcNow;

                decimal actualClosingCash = 0m;
                foreach (var input in request.ClosingCashCounts)
                {
                    var subtotal = denomFaceById[input.DenominationId] * input.Quantity;
                    actualClosingCash += subtotal;
                    session.CashCounts.Add(new ShiftSessionCashCount
                    {
                        ShiftSessionId = session.Id,
                        DenominationId = input.DenominationId,
                        Phase = ShiftSessionCashPhase.Closing,
                        Quantity = input.Quantity,
                        Subtotal = subtotal,
                        CreatedAt = now
                    });
                }

                // v1 placeholder: Expected = OpeningCash (no ticket payment SUM yet).
                // TODO: when TicketPaymentDetail feature ready, change to:
                //   Expected = OpeningCash + SUM(cash payments in this session)
                session.ExpectedClosingCash = session.OpeningCash ?? 0m;
                session.ActualClosingCash = actualClosingCash;
                session.Variance = actualClosingCash - session.ExpectedClosingCash;
            }

            // 4. Close
            var closedAt = clock.UtcNow;
            session.Status = ShiftSessionStatus.Closed;
            session.ClosedAt = closedAt;
            session.UpdatedAt = closedAt;

            // 5. Audit log
            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(ShiftSession),
                EntityId = session.Id,
                Action = "CLOSE_SHIFT",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = closedAt,
                Summary = BuildAuditSummary(session)
            });

            await dbContext.SaveChangesAsync(ct);

            // 6. Re-load with Denomination navigation for accurate summary
            var summary = await dbContext.ShiftSessions
                .Include(x => x.CashCounts).ThenInclude(c => c.Denomination)
                .FirstAsync(x => x.Id == session.Id, ct);

            return Result.Success(new Response(ShiftSessionMapper.Map(summary)));
        }

        private static string BuildAuditSummary(ShiftSession s) =>
            s.HasCashTracking
                ? $"Close ca | Expected={s.ExpectedClosingCash:N0}đ | " +
                  $"Actual={s.ActualClosingCash:N0}đ | Variance={s.Variance:N0}đ"
                : "Close ca";
    }
}
