using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Payments;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Configuration;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;
using Swashbuckle.AspNetCore.Annotations;

namespace Rpom.Application.Payments.CreateQrPayment;
public static class CreateQrPayment
{
    public sealed record Command(long TicketId, long Amount, string? Notes) : ICommand<Response>;

    public sealed record Response(
        long PaymentId,
        long TicketId,
        [property: SwaggerSchema("Số tiền thanh toán qua QR.")]
        long Amount,
        [property: SwaggerSchema("Trạng thái hiện tại của payment (PENDING, SUCCESS, FAILED).")]
        string Status,
        [property: SwaggerSchema("Mã tham chiếu duy nhất để đối soát giao dịch ngân hàng.")]
        string ReferenceCode,
        [property: SwaggerSchema("URL ảnh QR để khách hàng quét thanh toán.")]
        string QrImageUrl,
        string AccountNumber,
        string BankCode,
        string AccountName,
        [property: SwaggerSchema("Tổng số tiền của ticket.")]
        long TotalAmount,
        [property: SwaggerSchema("Số tiền đã thanh toán thành công (không tính mới tạo).")]
        long PaidAmount,
        [property: SwaggerSchema("Số tiền còn lại chưa thanh toán (chỉ trừ payments có status SUCCESS, không trừ mới tạo).")]
        long RemainingAmount,
        [property: SwaggerSchema("Số tiền thừa/hoàn lại của toàn bộ hóa đơn.")]
        long RefundAmount,
        bool IsFullyPaid,
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
        ICurrentTerminal currentTerminal,
        IQrPaymentGateway gateway,
        IRefreshPaymentTotalsService refreshService,
        IDateTimeProvider clock,
        IConfigValueService config,
        IVersionService versionService) : ICommandHandler<Command, Response>
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

            var table = await dbContext.Tables
                .Where(t => t.Id == ticket.TableId && t.IsActive)
                .Select(t => new
                {
                    t.Id,
                    t.AreaId,
                    t.Area.CounterId,
                    t.Area.ServiceChargePercent,
                    t.Area.ServiceChargeVatPercent
                }).FirstOrDefaultAsync(ct);
            if (table is null)
                return Result.Failure<Response>(PaymentErrors.TicketNotFound);

            var drawer = await dbContext.CashDrawerSessions
                .Where(d => d.CounterId == table.CounterId && d.Status == CashDrawerStatus.Open)
                .Select(d => new { d.Id, d.ShiftId })
                .FirstOrDefaultAsync(ct);
            if (drawer is null)
            {
                return Result.Failure<Response>(PaymentErrors.CashDrawerSessionsNotOpen);
            }

            var now = clock.UtcNow;
            var remainingAmount = ticket.TotalAmount - ticket.PaidAmount;

            if (remainingAmount <= 0)
                return Result.Failure<Response>(PaymentErrors.NothingToPay);

            var existingPending = ticket.Payments.FirstOrDefault(p => p.Status == TicketPaymentStatus.Pending);
            if (existingPending is not null)
            {
                if (existingPending.ExpiresAt is { } exp && exp < now)
                {
                    existingPending.Status = TicketPaymentStatus.Cancelled;
                    existingPending.ProcessedAt = now;
                    existingPending.UpdatedAt = now;
                }
                else
                {
                    return Result.Failure<Response>(PaymentErrors.PendingPaymentExists);
                }
            }

            if (request.Amount > remainingAmount)
                return Result.Failure<Response>(PaymentErrors.AmountExceedsRemaining);

            var method = await dbContext.PaymentMethods
                .FirstOrDefaultAsync(x => x.Code == PaymentMethodCodes.Qr && x.IsActive, ct);
            if (method is null)
                return Result.Failure<Response>(PaymentErrors.PaymentMethodMissing);

            // From header X-Terminal-Token. Not send token → null (sync cash); wrong token → error
            int? posTerminalId = null;
            if (currentTerminal.TerminalToken is { } termToken)
            {
                posTerminalId = await dbContext.PosTerminals
                    .Where(t => t.DeviceToken == termToken && t.IsActive)
                    .Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
                if (posTerminalId is null)
                    return Result.Failure<Response>(PosTerminalErrors.InvalidToken);
            }

            var staffId = currentStaff.StaffAccountId;

            int ttlSeconds = await config.GetIntAsync(ConfigCodes.PaymentQrTtlSeconds, 300, ct);
            DateTime? expiresAt = ttlSeconds > 0 ? now.AddSeconds(ttlSeconds) : null;

            var payment = new TicketPaymentDetail
            {
                TicketId = ticket.Id,
                PaymentMethodId = method.Id,
                Amount = request.Amount,
                Status = TicketPaymentStatus.Pending,
                ProcessedByStaffId = staffId,
                ExpiresAt = expiresAt,
                PosTerminalId = posTerminalId,
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

            await refreshService.RefreshAsync(ticket.Id, ct);
            await dbContext.SaveChangesAsync(ct);

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

            var finalRemaining = ticket.TotalAmount - ticket.PaidAmount;

            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Payment.CreateQR(id={payment.Id})", ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"Payment.CreateQR(id={payment.Id})", ct);

            return Result.Success(new Response(
                payment.Id, ticket.Id, (long)payment.Amount, payment.Status,
                qr.ReferenceCode, qr.QrImageUrl, qr.AccountNumber, qr.BankCode, qr.AccountName,
                (long)ticket.TotalAmount,
                (long)ticket.PaidAmount,
                (long)(remainingAmount < 0 ? 0 : remainingAmount),
                (long)ticket.RefundAmount,
                finalRemaining <= 0,
                now));
        }
    }
}
