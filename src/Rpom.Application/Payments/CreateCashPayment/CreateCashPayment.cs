using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;
using Swashbuckle.AspNetCore.Annotations;

namespace Rpom.Application.Payments.CreateCashPayment;

public static class CreateCashPayment
{
    public sealed record Command(
        long TicketId,
        decimal Amount,
        decimal ReceivedAmount,
        string? Notes) : ICommand<Response>;

    public sealed record Response(
        long PaymentId,
        long TicketId,
        [property: SwaggerSchema("Số tiền khách đưa.")]
        decimal ReceivedAmount,
        [property: SwaggerSchema("Số tiền thực tế cấn trừ.")]
        decimal Amount,
        [property: SwaggerSchema("Tiền thối lại cho khách ngay tại giao dịch này.")]
        decimal ChangeAmount,
        string Status,
        decimal TotalAmount,
        [property: SwaggerSchema("Tổng tiền đã thanh toán của hóa đơn.")]
        decimal TicketPaidAmount,
        decimal RemainingAmount,
        [property: SwaggerSchema("Số tiền thừa/hoàn lại của toàn bộ hóa đơn.")]
        decimal RefundAmount,
        bool IsFullyPaid,
        DateTime ProcessedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);
            // Amount = số tiền cấn trừ vào hoá đơn. >= 0 (0 = giao dịch hoàn tiền thuần).
            RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Notes).MaximumLength(500);

            // Thanh toán bình thường: khách phải đưa >= số tiền cấn trừ.
            RuleFor(x => x.ReceivedAmount)
                .GreaterThanOrEqualTo(x => x.Amount)
                .When(x => x.Amount > 0)
                .WithMessage("Số tiền khách đưa phải >= số tiền cấn trừ vào hoá đơn.");

            // Giao dịch hoàn tiền: Amount = 0 và ReceivedAmount < 0 (tiền chi ra cho khách).
            RuleFor(x => x.ReceivedAmount)
                .LessThan(0)
                .When(x => x.Amount == 0)
                .WithMessage("Hoàn tiền: số tiền cấn trừ = 0 và số tiền (âm) là tiền hoàn cho khách.");
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IRefreshPaymentTotalsService refreshService,
        IDateTimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var ticket = await dbContext.Tickets
                .Include(t => t.Payments)
                .FirstOrDefaultAsync(t => t.Id == request.TicketId, ct);

            if (ticket is null)
                return Result.Failure<Response>(PaymentErrors.TicketNotFound);
            if (ticket.Status != TicketStatus.Open)
                return Result.Failure<Response>(PaymentErrors.TicketNotOpen);

            // Amount = 0 → giao dịch hoàn tiền thuần (thối phần khách đã trả thừa).
            var isRefund = request.Amount == 0m;
            var remainingAmount = ticket.TotalAmount - ticket.PaidAmount;

            if (ticket.Payments.Any(p => p.Status == TicketPaymentStatus.Pending))
                return Result.Failure<Response>(PaymentErrors.PendingPaymentExists);

            if (!isRefund)
            {
                if (remainingAmount <= 0)
                    return Result.Failure<Response>(PaymentErrors.NothingToPay);
                if (request.Amount > remainingAmount)
                    return Result.Failure<Response>(PaymentErrors.AmountExceedsRemaining);
            }
            // else: giao dịch hoàn tiền thuần (Amount = 0, ReceivedAmount < 0).
            // Đây là thao tác thủ công của Cashier để thối cho khách trong các trường hợp
            // khách chuyển khoản sai/dư qua QR (webhook chỉ ghi nhận transaction, không cộng
            // tiền). Cashier đối chiếu với bảng payment_transactions để xác định số cần hoàn,
            // nên KHÔNG ràng buộc theo ticket.RefundAmount. Dòng payment âm sẽ làm giảm
            // PaidAmount tương ứng.

            // Tiền thối tại quầy = phần khách đưa dư so với số cấn trừ (chỉ tính khi > 0).
            var changeAmount = request.ReceivedAmount - request.Amount;

            var method = await dbContext.PaymentMethods
                .FirstOrDefaultAsync(x => x.Code == PaymentMethodCodes.Cash && x.IsActive, ct);
            if (method is null)
                return Result.Failure<Response>(PaymentErrors.PaymentMethodMissing);

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;

            // Dòng 1: số tiền khách đưa (ReceivedAmount). Khi hoàn tiền giá trị này âm.
            var payment = new TicketPaymentDetail
            {
                TicketId = ticket.Id,
                PaymentMethodId = method.Id,
                Amount = request.ReceivedAmount,
                Status = TicketPaymentStatus.Success,
                ProcessedAt = now,
                ProcessedByStaffId = staffId,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            };
            dbContext.TicketPaymentDetails.Add(payment);

            // Dòng 2: tiền thối lại cho khách — amount âm (= -changeAmount). Chỉ tạo khi có thối.
            // Trỏ về dòng gốc qua navigation để khi xoá sẽ xoá cả cụm cùng lúc.
            if (changeAmount > 0)
            {
                dbContext.TicketPaymentDetails.Add(new TicketPaymentDetail
                {
                    TicketId = ticket.Id,
                    PaymentMethodId = method.Id,
                    Amount = -changeAmount,
                    Status = TicketPaymentStatus.Success,
                    ProcessedAt = now,
                    ProcessedByStaffId = staffId,
                    Notes = "Tiền thối lại cho khách",
                    ParentPaymentDetail = payment,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<Response>(PaymentErrors.ConcurrencyConflict);
            }

            await refreshService.RefreshAsync(ticket.Id, ct);

            await dbContext.SaveChangesAsync(ct);

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            string logSummary;
            if (isRefund)
                logSummary = $"Hoàn tiền mặt {(-request.ReceivedAmount):N0}đ cho khách của hoá đơn {ticket.Code}";
            else if (changeAmount > 0)
                logSummary = $"Thanh toán tiền mặt: Khách đưa {request.ReceivedAmount:N0}đ, thực thu {request.Amount:N0}đ, thối lại {changeAmount:N0}đ cho hoá đơn {ticket.Code}";
            else
                logSummary = $"Thanh toán tiền mặt {request.Amount:N0}đ cho hoá đơn {ticket.Code}";

            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = ticket.Id,
                Action = "PAYMENT_CASH",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = logSummary,
            });
            await dbContext.SaveChangesAsync(ct);

            var finalRemaining = ticket.TotalAmount - ticket.PaidAmount;

            return Result.Success(new Response(
                payment.Id,
                ticket.Id,
                request.ReceivedAmount,
                request.Amount,
                ticket.ChangeAmount,
                payment.Status,
                ticket.TotalAmount,
                ticket.PaidAmount,
                finalRemaining < 0 ? 0 : finalRemaining,
                ticket.RefundAmount,
                finalRemaining <= 0,
                now));
        }
    }
}
