using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Payments.CancelPendingPayment;
public static class CancelPendingPayment
{
    public sealed record Command(long PaymentId, string? Reason) : ICommand<Response>;

    public sealed record Response(long PaymentId, string Status);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PaymentId).GreaterThan(0);
            RuleFor(x => x.Reason).MaximumLength(500);
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var payment = await dbContext.TicketPaymentDetails
                .FirstOrDefaultAsync(x => x.Id == request.PaymentId, ct);

            if (payment is null)
                return Result.Failure<Response>(PaymentErrors.PaymentNotFound);
            if (payment.Status != TicketPaymentStatus.Pending)
                return Result.Failure<Response>(PaymentErrors.PaymentNotPending);

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;

            payment.Status = TicketPaymentStatus.Cancelled;
            payment.ProcessedAt = now;
            payment.UpdatedAt = now;
            if (!string.IsNullOrWhiteSpace(request.Reason))
                payment.Notes = request.Reason.Trim();

            var ticket = await dbContext.Tickets.FirstAsync(x => x.Id == payment.TicketId, ct);
            ticket.UpdatedAt = now;

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = payment.TicketId,
                Action = "PAYMENT_CANCEL",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Huỷ thanh toán chờ #{payment.Id} ({payment.Amount:N0}đ)",
            });

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<Response>(PaymentErrors.ConcurrencyConflict);
            }

            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Payment.CancelPendingPayment(id={payment.Id})", ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"Payment.CancelPendingPayment(id={payment.Id})", ct);

            return Result.Success(new Response(payment.Id, payment.Status));
        }
    }
}
