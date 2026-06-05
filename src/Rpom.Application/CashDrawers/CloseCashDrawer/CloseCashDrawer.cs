using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Sales.CashDrawer;

namespace Rpom.Application.CashDrawers.CloseCashDrawer;

/// <summary>
/// Close a cash drawer. Permission <c>cash_drawer:close</c> required — any
/// holder of that permission can close, including a different staff than
/// the one who opened (e.g. Manager force-close, or shift hand-over).
/// Variance is logged but never blocks closure.
/// </summary>
public static class CloseCashDrawer
{
    public sealed record CashCountInput(int DenominationId, int Quantity);

    public sealed record Command(
        long Id,
        IReadOnlyList<CashCountInput> ClosingCashCounts,
        string? Notes) : ICommand<Response>;

    public sealed record Response(
        long Id,
        int CounterId,
        int OpenedByStaffAccountId,
        int ClosedByStaffAccountId,
        DateTime OpenedAt,
        DateTime ClosedAt,
        decimal OpeningCash,
        decimal ExpectedClosingCash,
        decimal ActualClosingCash,
        decimal Variance,
        string Status,
        IReadOnlyList<CashCountLine> ClosingCashCounts);

    public sealed record CashCountLine(int DenominationId, int Quantity, decimal FaceValue, decimal Subtotal);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.ClosingCashCounts).NotEmpty();
            RuleForEach(x => x.ClosingCashCounts).ChildRules(c =>
            {
                c.RuleFor(x => x.DenominationId).GreaterThan(0);
                c.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
            });
            RuleFor(x => x.Notes).MaximumLength(500);
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var entity = await dbContext.CashDrawerSessions
                .Include(x => x.CashCounts)
                .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null) return Result.Failure<Response>(CashDrawerErrors.NotFound);
            if (entity.Status != CashDrawerStatus.Open) return Result.Failure<Response>(CashDrawerErrors.NotOpen);

            if (request.ClosingCashCounts.Count == 0)
                return Result.Failure<Response>(CashDrawerErrors.CashCountsRequired);

            var requestedIds = request.ClosingCashCounts.Select(x => x.DenominationId).Distinct().ToList();
            var denoms = await dbContext.Denominations
                .Where(d => requestedIds.Contains(d.Id) && d.IsActive)
                .ToListAsync(ct);
            if (denoms.Count != requestedIds.Count)
                return Result.Failure<Response>(CashDrawerErrors.DenominationInvalid);

            var faceById = denoms.ToDictionary(d => d.Id, d => d.FaceValue);
            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;

            decimal actual = 0m;
            foreach (var input in request.ClosingCashCounts)
            {
                var subtotal = faceById[input.DenominationId] * input.Quantity;
                actual += subtotal;
                entity.CashCounts.Add(new CashDrawerCashCount
                {
                    CashDrawerSessionId = entity.Id,
                    DenominationId = input.DenominationId,
                    Phase = CashDrawerCashPhase.Closing,
                    Quantity = input.Quantity,
                    Subtotal = subtotal,
                    CreatedAt = now,
                });
            }

            // v1: expected = opening (no payment integration yet). Will become
            // opening + Σ cash payments - Σ change once payment feature lands.
            var expected = entity.OpeningCash;

            entity.ClosedByStaffAccountId = staffId;
            entity.ClosedAt = now;
            entity.ExpectedClosingCash = expected;
            entity.ActualClosingCash = actual;
            entity.Variance = actual - expected;
            entity.Status = CashDrawerStatus.Closed;
            entity.UpdatedAt = now;
            if (!string.IsNullOrWhiteSpace(request.Notes))
                entity.Notes = string.IsNullOrWhiteSpace(entity.Notes)
                    ? request.Notes.Trim()
                    : entity.Notes + "\n---\n" + request.Notes.Trim();

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(CashDrawerSession),
                EntityId = entity.Id,
                Action = "CLOSE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Cash drawer closed | expected={expected:N0}đ | actual={actual:N0}đ | variance={entity.Variance:N0}đ",
            });

            await dbContext.SaveChangesAsync(ct);

            var lines = entity.CashCounts
                .Where(c => c.Phase == CashDrawerCashPhase.Closing)
                .Select(c => new CashCountLine(
                    c.DenominationId, c.Quantity, faceById[c.DenominationId], c.Subtotal))
                .ToList();

            return Result.Success(new Response(
                entity.Id, entity.CounterId,
                entity.OpenedByStaffAccountId, staffId,
                entity.OpenedAt, entity.ClosedAt!.Value,
                entity.OpeningCash, expected, actual, entity.Variance.GetValueOrDefault(),
                entity.Status, lines));
        }
    }
}
