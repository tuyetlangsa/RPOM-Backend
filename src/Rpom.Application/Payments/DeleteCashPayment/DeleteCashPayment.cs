using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Payments.DeleteCashPayment;
public static class DeleteCashPayment
{
    public sealed record Command(long PaymentId, string? Reason) : ICommand<Response>;

    public sealed record Response(
        long TicketId,
        IReadOnlyList<long> DeletedPaymentIds,
        decimal PaidAmount,
        decimal RemainingAmount,
        decimal RefundAmount);

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
        IRefreshPaymentTotalsService refreshService,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var payment = await dbContext.TicketPaymentDetails
                .FirstOrDefaultAsync(x => x.Id == request.PaymentId, ct);
            if (payment is null)
                return Result.Failure<Response>(PaymentErrors.PaymentNotFound);

            if (payment.Status != TicketPaymentStatus.Success)
                return Result.Failure<Response>(PaymentErrors.PaymentNotSuccess);

            if (payment.Amount <= 0)
                return Result.Failure<Response>(PaymentErrors.PaymentNotDeletableNegative);

            var group = await dbContext.TicketPaymentDetails
                .Where(p => p.Id == payment.Id || p.ParentPaymentDetailId == payment.Id)
                .ToListAsync(ct);

            var cashMethod = await dbContext.PaymentMethods
                .FirstOrDefaultAsync(x => x.Code == PaymentMethodCodes.Cash, ct);
            if (cashMethod is null)
                return Result.Failure<Response>(PaymentErrors.PaymentMethodMissing);

            if (group.Any(p => p.PaymentMethodId != cashMethod.Id))
                return Result.Failure<Response>(PaymentErrors.PaymentNotCash);

            var ticket = await dbContext.Tickets.FirstAsync(x => x.Id == payment.TicketId, ct);
            if (ticket.Status != TicketStatus.Open)
                return Result.Failure<Response>(PaymentErrors.TicketNotOpen);

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

            var deletedIds = new List<long>();
            foreach (var row in group)
            {
                // Bỏ qua các dòng trong cụm đã ở trạng thái terminal khác (an toàn).
                if (row.Status != TicketPaymentStatus.Success) continue;
                row.Status = TicketPaymentStatus.Deleted;
                row.UpdatedAt = now;
                if (reason is not null)
                    row.Notes = string.IsNullOrWhiteSpace(row.Notes) ? reason : $"{row.Notes} | {reason}";
                deletedIds.Add(row.Id);
            }

            await dbContext.SaveChangesAsync(ct);

            await refreshService.RefreshAsync(ticket.Id, ct);

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = ticket.Id,
                Action = "PAYMENT_DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Xoá giao dịch tiền mặt [{string.Join(", ", deletedIds)}] của hoá đơn {ticket.Code}"
                          + (reason is null ? "" : $" — lý do: {reason}"),
            });

            await dbContext.SaveChangesAsync(ct);

            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Payment.DeleteCashPayment(id={payment.Id})", ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"Payment.DeleteCashPayment(id={payment.Id})", ct);

            var remaining = ticket.TotalAmount - ticket.PaidAmount;
            return Result.Success(new Response(
                ticket.Id,
                deletedIds,
                ticket.PaidAmount,
                remaining < 0 ? 0 : remaining,
                ticket.RefundAmount));
        }
    }
}
