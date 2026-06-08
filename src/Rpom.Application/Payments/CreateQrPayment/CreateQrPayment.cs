using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Payments;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;
using Swashbuckle.AspNetCore.Annotations;

namespace Rpom.Application.Payments.CreateQrPayment;
public static class CreateQrPayment
{
    public sealed record Command(long TicketId, decimal Amount, string? Notes) : ICommand<Response>;

    public sealed record Response(
        long PaymentId,
        long TicketId,
        [SwaggerSchema("Số tiền thanh toán qua QR.")]
        decimal Amount,
        [SwaggerSchema("Trạng thái hiện tại của payment (PENDING, SUCCESS, FAILED).")]
        string Status,
        [SwaggerSchema("Mã tham chiếu duy nhất để đối soát giao dịch ngân hàng.")]
        string ReferenceCode,
        [SwaggerSchema("URL ảnh QR để khách hàng quét thanh toán.")]
        string QrImageUrl,
        string AccountNumber,
        string BankCode,
        string AccountName,
        [SwaggerSchema("Tổng số tiền của ticket.")]
        decimal TotalAmount,
        [SwaggerSchema("Số tiền đã thanh toán thành công (không tính mới tạo).")]
        decimal PaidAmount,
        [SwaggerSchema("Số tiền còn lại chưa thanh toán (chỉ trừ payments có status SUCCESS, không trừ mới tạo).")]
        decimal RemainingAmount,
        DateTime CreatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);
            RuleFor(x => x.Amount).GreaterThan(0);
            RuleFor(x => x.Notes).MaximumLength(500);
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IQrPaymentGateway gateway,
        IDateTimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            if (!gateway.IsConfigured)
                return Result.Failure<Response>(PaymentErrors.QrGatewayUnavailable);

            var ticket = await dbContext.Tickets
                .Include(t => t.Payments)
                .FirstOrDefaultAsync(t => t.Id == request.TicketId, ct);

            if (ticket is null)
                return Result.Failure<Response>(PaymentErrors.TicketNotFound);
            if (ticket.Status != TicketStatus.Open)
                return Result.Failure<Response>(PaymentErrors.TicketNotOpen);

            var total = ticket.TotalAmount;
            var paid = ticket.PaidAmount;
            var remainingAmount = total - paid;

            if (remainingAmount <= 0)
                return Result.Failure<Response>(PaymentErrors.NothingToPay);
            if (ticket.Payments.Any(p => p.Status == TicketPaymentStatus.Pending))
                return Result.Failure<Response>(PaymentErrors.PendingPaymentExists);
            if (request.Amount > remainingAmount)
                return Result.Failure<Response>(PaymentErrors.AmountExceedsRemaining);

            var method = await dbContext.PaymentMethods
                .FirstOrDefaultAsync(x => x.Code == PaymentMethodCodes.Qr && x.IsActive, ct);
            if (method is null)
                return Result.Failure<Response>(PaymentErrors.PaymentMethodMissing);

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;

            var payment = new TicketPaymentDetail
            {
                TicketId = ticket.Id,
                PaymentMethodId = method.Id,
                Amount = request.Amount,
                Status = TicketPaymentStatus.Pending,
                ProcessedByStaffId = staffId,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            };
            dbContext.TicketPaymentDetails.Add(payment);

            ticket.UpdatedAt = now;
            //ticket.Version++;

            try
            {
                await dbContext.SaveChangesAsync(ct); // need payment.Id for the reference code
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<Response>(PaymentErrors.ConcurrencyConflict);
            }

            var reference = gateway.BuildReferenceCode(payment.Id);
            var qr = gateway.BuildQrCode(reference, payment.Amount);

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = ticket.Id,
                Action = "PAYMENT_QR_INIT",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Khởi tạo thanh toán QR {payment.Amount:N0}đ (ref={reference}) cho hoá đơn {ticket.Code}",
            });
            await dbContext.SaveChangesAsync(ct);

            return Result.Success(new Response(
                payment.Id, ticket.Id, payment.Amount, payment.Status,
                qr.ReferenceCode, qr.QrImageUrl, qr.AccountNumber, qr.BankCode, qr.AccountName,
                total, paid,
                remainingAmount < 0 ? 0 : remainingAmount,
                now));
        }
    }
}
