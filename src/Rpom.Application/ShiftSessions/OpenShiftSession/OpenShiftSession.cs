using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Authentication;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.ShiftSessions.Shared;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.ShiftSessions.OpenShiftSession;

public static class OpenShiftSession
{
    public sealed record CashCountInput(int DenominationId, int Quantity);

    public sealed record Command(
        int ShiftId,
        int? CounterId,
        int? KitchenStationId,
        bool HasCashTracking,
        IReadOnlyList<CashCountInput>? OpeningCashCounts) : ICommand<Response>;

    public sealed record Response(
        ShiftSessionSummary ShiftSession,
        string AccessToken,
        DateTime ExpiresAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ShiftId).GreaterThan(0);
            RuleFor(x => x.OpeningCashCounts)
                .Must(list => list is null || list.All(c => c.Quantity >= 0))
                .WithMessage("Quantity của mỗi cash count phải ≥ 0.");
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IJwtTokenService jwtTokenService,
        IDateTimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            // 1. Load staff + role
            var staffId = currentStaff.StaffAccountId;
            var staff = await dbContext.StaffAccounts
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.Id == staffId, ct);

            if (staff is null) return Result.Failure<Response>(ShiftSessionErrors.NotFound);

            // 2. Role → scope mapping enforcement
            var scopeCheck = ShiftScopeRules.Validate(
                staff.Role.Code, request.CounterId, request.KitchenStationId, request.HasCashTracking);
            if (scopeCheck.IsFailure) return Result.Failure<Response>(scopeCheck.Error);

            // 3. Validate ShiftId + spatial scope exist + active
            var shiftExists = await dbContext.Shifts.AnyAsync(
                x => x.Id == request.ShiftId && x.IsActive, ct);
            if (!shiftExists) return Result.Failure<Response>(ShiftSessionErrors.ShiftDefinitionInvalid);

            if (request.CounterId.HasValue)
            {
                var ok = await dbContext.Counters.AnyAsync(
                    x => x.Id == request.CounterId.Value && x.IsActive, ct);
                if (!ok) return Result.Failure<Response>(ShiftSessionErrors.CounterInvalid);
            }

            if (request.KitchenStationId.HasValue)
            {
                var ok = await dbContext.KitchenStations.AnyAsync(
                    x => x.Id == request.KitchenStationId.Value && x.IsActive, ct);
                if (!ok) return Result.Failure<Response>(ShiftSessionErrors.KitchenStationInvalid);
            }

            // 4. Pre-check uniqueness (DB filtered unique is the final safety net for races)
            var staffHasOpen = await dbContext.ShiftSessions
                .AnyAsync(x => x.StaffAccountId == staffId && x.Status == ShiftSessionStatus.Open, ct);
            if (staffHasOpen)
                return Result.Failure<Response>(ShiftSessionErrors.AlreadyOpen);

            if (request.HasCashTracking && request.CounterId.HasValue)
            {
                var counterOccupied = await dbContext.ShiftSessions.AnyAsync(
                    x => x.CounterId == request.CounterId
                         && x.HasCashTracking
                         && x.Status == ShiftSessionStatus.Open, ct);
                if (counterOccupied)
                    return Result.Failure<Response>(ShiftSessionErrors.CounterCashierOccupied);
            }

            // 5. Cash count validation
            decimal? openingCash = null;
            Dictionary<int, decimal> denomFaceById = new();

            if (request.HasCashTracking)
            {
                if (request.OpeningCashCounts is null || request.OpeningCashCounts.Count == 0)
                    return Result.Failure<Response>(ShiftSessionErrors.CashCountsRequired);

                var requestedIds = request.OpeningCashCounts.Select(x => x.DenominationId).Distinct().ToList();
                var denoms = await dbContext.Denominations
                    .Where(x => requestedIds.Contains(x.Id) && x.IsActive)
                    .ToListAsync(ct);

                if (denoms.Count != requestedIds.Count)
                    return Result.Failure<Response>(ShiftSessionErrors.DenominationInvalid);

                denomFaceById = denoms.ToDictionary(d => d.Id, d => d.FaceValue);
                openingCash = request.OpeningCashCounts.Sum(c => denomFaceById[c.DenominationId] * c.Quantity);
            }

            // 6. Create ShiftSession + child cash count rows
            var now = clock.UtcNow;
            var session = new ShiftSession
            {
                ShiftId = request.ShiftId,
                StaffAccountId = staffId,
                CounterId = request.CounterId,
                KitchenStationId = request.KitchenStationId,
                HasCashTracking = request.HasCashTracking,
                OpenedAt = now,
                Status = ShiftSessionStatus.Open,
                OpeningCash = openingCash,
                CreatedAt = now,
                UpdatedAt = now
            };

            if (request.HasCashTracking)
            {
                foreach (var input in request.OpeningCashCounts!)
                {
                    session.CashCounts.Add(new ShiftSessionCashCount
                    {
                        DenominationId = input.DenominationId,
                        Phase = ShiftSessionCashPhase.Opening,
                        Quantity = input.Quantity,
                        Subtotal = denomFaceById[input.DenominationId] * input.Quantity,
                        CreatedAt = now
                    });
                }
            }

            dbContext.ShiftSessions.Add(session);
            await dbContext.SaveChangesAsync(ct); // assigns session.Id

            // 7. Audit log (after we have session.Id)
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(ShiftSession),
                EntityId = session.Id,
                Action = "OPEN_SHIFT",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = BuildAuditSummary(request, openingCash)
            });
            await dbContext.SaveChangesAsync(ct);

            // 8. Re-issue JWT with shift scope claims
            var token = jwtTokenService.IssueAccessToken(
                staffId,
                staff.Username,
                new ShiftScopeClaims(session.Id, session.CounterId, session.KitchenStationId));

            // 9. Re-load session with Denomination navigation populated for accurate summary mapping.
            var summary = await ShiftSessionMapper.LoadCurrentForStaffAsync(dbContext, staffId, ct);

            return Result.Success(new Response(
                ShiftSession: summary!,
                AccessToken: token.Token,
                ExpiresAt: token.ExpiresAt));
        }

        private static string BuildAuditSummary(Command cmd, decimal? openingCash)
        {
            var scope = cmd.CounterId.HasValue
                ? $"Counter#{cmd.CounterId}"
                : $"KitchenStation#{cmd.KitchenStationId}";
            return openingCash.HasValue
                ? $"Open ca | {scope} | Shift#{cmd.ShiftId} | OpeningCash={openingCash:N0}đ"
                : $"Open ca | {scope} | Shift#{cmd.ShiftId}";
        }
    }
}
