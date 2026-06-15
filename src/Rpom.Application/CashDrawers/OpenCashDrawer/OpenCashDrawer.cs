using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;

namespace Rpom.Application.CashDrawers.OpenCashDrawer;

/// <summary>
///     Open a cash drawer at a counter. Permission <c>cash_drawer:open</c> required.
///     Refuses if any cash drawer at this counter is already OPEN.
///     Computes OpeningCash from the provided denomination counts.
/// </summary>
public static class OpenCashDrawer
{
    public sealed record CashCountInput(int DenominationId, int Quantity);

    public sealed record Command(
        int CounterId,
        int ShiftId,
        IReadOnlyList<CashCountInput> OpeningCashCounts,
        string? Notes) : ICommand<Response>;

    public sealed record Response(
        long Id,
        int CounterId,
        int OpenedByStaffAccountId,
        DateTime OpenedAt,
        decimal OpeningCash,
        string Status,
        IReadOnlyList<CashCountLine> OpeningCashCounts);

    public sealed record CashCountLine(int DenominationId, int Quantity, decimal FaceValue, decimal Subtotal);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.CounterId).GreaterThan(0);
            RuleFor(x => x.ShiftId).GreaterThan(0);
            RuleFor(x => x.OpeningCashCounts).NotEmpty();
            RuleForEach(x => x.OpeningCashCounts).ChildRules(c =>
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
            // Counter must exist + active
            Counter? counter = await dbContext.Counters.FirstOrDefaultAsync(x => x.Id == request.CounterId, ct);
            if (counter is null || !counter.IsActive)
            {
                return Result.Failure<Response>(CashDrawerErrors.CounterInvalid);
            }

            // 1 OPEN drawer per counter — pre-check (filtered unique index is the safety net)
            bool alreadyOpen = await dbContext.CashDrawerSessions
                .AnyAsync(x => x.CounterId == request.CounterId && x.Status == CashDrawerStatus.Open, ct);
            if (alreadyOpen)
            {
                return Result.Failure<Response>(CashDrawerErrors.CounterAlreadyOpen);
            }

            // Work shift must exist + active — Ticket.ShiftId is derived from this session.
            bool shiftValid = await dbContext.Shifts
                .AnyAsync(s => s.Id == request.ShiftId && s.IsActive, ct);
            if (!shiftValid)
            {
                return Result.Failure<Response>(CashDrawerErrors.ShiftInvalid);
            }

            // Validate denominations + compute opening cash
            var requestedIds = request.OpeningCashCounts.Select(x => x.DenominationId).Distinct().ToList();
            List<Denomination> denoms = await dbContext.Denominations
                .Where(d => requestedIds.Contains(d.Id) && d.IsActive)
                .ToListAsync(ct);
            if (denoms.Count != requestedIds.Count)
            {
                return Result.Failure<Response>(CashDrawerErrors.DenominationInvalid);
            }

            var faceById = denoms.ToDictionary(d => d.Id, d => d.FaceValue);
            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            var entity = new CashDrawerSession
            {
                CounterId = request.CounterId,
                ShiftId = request.ShiftId,
                OpenedByStaffAccountId = staffId,
                OpenedAt = now,
                Status = CashDrawerStatus.Open,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            decimal opening = 0m;
            foreach (CashCountInput input in request.OpeningCashCounts)
            {
                decimal subtotal = faceById[input.DenominationId] * input.Quantity;
                opening += subtotal;
                entity.CashCounts.Add(new CashDrawerCashCount
                {
                    DenominationId = input.DenominationId,
                    Phase = CashDrawerCashPhase.Opening,
                    Quantity = input.Quantity,
                    Subtotal = subtotal,
                    CreatedAt = now
                });
            }

            entity.OpeningCash = opening;

            dbContext.CashDrawerSessions.Add(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            await dbContext.SaveChangesAsync(ct);

            List<Ticket> orphanTickets = await dbContext.Tickets
                .Where(t => t.CounterId == request.CounterId
                            && t.Status == TicketStatus.Open
                            && t.CashDrawerSessionId != entity.Id)
                .ToListAsync(ct);
            foreach (Ticket t in orphanTickets)
            {
                t.CashDrawerSessionId = entity.Id;
                t.UpdatedAt = now;
            }

            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(CashDrawerSession),
                EntityId = entity.Id,
                Action = "OPEN",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Cash drawer opened at counter={counter.Name} | opening={opening:N0}đ"
                          + (orphanTickets.Count > 0 ? $" | carried over {orphanTickets.Count} open ticket(s)" : "")
            });
            await dbContext.SaveChangesAsync(ct);

            var lines = entity.CashCounts
                .Select(c => new CashCountLine(
                    c.DenominationId, c.Quantity, faceById[c.DenominationId], c.Subtotal))
                .ToList();

            return Result.Success(new Response(
                entity.Id, entity.CounterId, entity.OpenedByStaffAccountId,
                entity.OpenedAt, entity.OpeningCash, entity.Status, lines));
        }
    }
}
