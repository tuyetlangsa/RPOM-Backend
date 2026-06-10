using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Payments;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Payments.HandleSePayWebhook;
public static class HandleSePayWebhook
{
    public sealed record Command(
        string? Signature,
        string? Timestamp,
        long Id,
        string? Gateway,
        string? TransactionDate,
        string? AccountNumber,
        string? SubAccount,
        string? Code,
        string? Content,
        string? TransferType,
        decimal TransferAmount,
        decimal Accumulated,
        string? ReferenceCode,
        string? Description,
        string? RawPayload) : ICommand<Response>;

    public sealed record Response(
        bool Success,
        string TransactionStatus,
        long? MatchedPaymentId,
        long? TicketId);

    internal sealed class Handler(
        IDbContext dbContext,
        IQrPaymentGateway gateway,
        IDateTimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            // Xác thực chữ ký HMAC-SHA256 trên raw body (RawPayload) + headers.
            if (!gateway.VerifyWebhookSignature(request.RawPayload, request.Signature, request.Timestamp))
                return Result.Failure<Response>(PaymentErrors.WebhookUnauthorized);

            var now = clock.UtcNow;

            // --- Idempotency: gateway transaction id already seen → no-op replay ---
            var existing = await dbContext.PaymentTransactions
                .FirstOrDefaultAsync(x => x.GatewayTransactionId == request.Id, ct);
            if (existing is not null)
            {
                return Result.Success(new Response(
                    true, existing.Status, existing.MatchedPaymentDetailId, null));
            }

            var txn = new PaymentTransaction
            {
                Gateway = string.IsNullOrWhiteSpace(request.Gateway) ? "SEPAY" : request.Gateway!,
                GatewayTransactionId = request.Id,
                BankBrand = request.Gateway,
                AccountNumber = request.AccountNumber,
                SubAccount = request.SubAccount,
                TransferType = string.IsNullOrWhiteSpace(request.TransferType) ? "in" : request.TransferType!,
                TransferAmount = request.TransferAmount,
                Accumulated = request.Accumulated,
                Code = request.Code,
                Content = request.Content,
                ReferenceCode = request.ReferenceCode,
                Description = request.Description,
                TransactionDate = ParseDateExact(request.TransactionDate, now),
                RawPayload = request.RawPayload,
                Status = PaymentTransactionStatus.Unmatched,
                CreatedAt = now,
                UpdatedAt = now,
            };
            dbContext.PaymentTransactions.Add(txn);

            // Only incoming transfers can settle a payment
            if (!string.Equals(txn.TransferType, "in", StringComparison.OrdinalIgnoreCase))
            {
                txn.Status = PaymentTransactionStatus.Ignored;
                await dbContext.SaveChangesAsync(ct);
                return Result.Success(new Response(true, txn.Status, null, null));
            }

            // Recover the payment id from the reference embedded in the memo
            var paymentId = gateway.TryParsePaymentDetailId(request.Code, request.Content);
            if (paymentId is null)
            {
                txn.Status = PaymentTransactionStatus.Unmatched;
                await dbContext.SaveChangesAsync(ct);
                return Result.Success(new Response(true, txn.Status, null, null));
            }

            txn.MatchedReferenceCode = gateway.BuildReferenceCode(paymentId.Value);

            var payment = await dbContext.TicketPaymentDetails
                .FirstOrDefaultAsync(x => x.Id == paymentId.Value, ct);

            if (payment is null)
            {
                txn.Status = PaymentTransactionStatus.Unmatched;
                await dbContext.SaveChangesAsync(ct);
                return Result.Success(new Response(true, txn.Status, null, null));
            }

            // Already settled (or terminal) → treat the webhook as a duplicate.
            if (payment.Status != TicketPaymentStatus.Pending)
            {
                txn.Status = PaymentTransactionStatus.Duplicate;
                txn.MatchedPaymentDetailId = payment.Id;
                await dbContext.SaveChangesAsync(ct);
                return Result.Success(new Response(true, txn.Status, payment.Id, payment.TicketId));
            }

            // Amount must match exactly — never over/under credit the ticket.
            if (request.TransferAmount != payment.Amount)
            {
                txn.Status = PaymentTransactionStatus.Mismatch;
                txn.MatchedPaymentDetailId = payment.Id;
                await dbContext.SaveChangesAsync(ct);
                return Result.Success(new Response(true, txn.Status, payment.Id, payment.TicketId));
            }

            // --- Settle ---
            payment.Status = TicketPaymentStatus.Success;
            payment.ProcessedAt = now;
            payment.TransactionRef = request.Id.ToString(CultureInfo.InvariantCulture);
            payment.UpdatedAt = now;

            txn.Status = PaymentTransactionStatus.Matched;
            txn.MatchedPaymentDetailId = payment.Id;
            txn.UpdatedAt = now;

            var ticket = await dbContext.Tickets.FirstAsync(x => x.Id == payment.TicketId, ct);
            ticket.PaidAmount += payment.Amount;
            ticket.UpdatedAt = now;
            //ticket.Version++;

            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Ticket),
                EntityId = ticket.Id,
                Action = "PAYMENT_QR_SUCCESS",
                ActorStaffAccountId = null,
                ActorFullName = "SePay Webhook",
                Timestamp = now,
                Summary = $"Thanh toán QR thành công {payment.Amount:N0}đ (SePay #{request.Id}) cho hoá đơn {ticket.Code}",
            });

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<Response>(PaymentErrors.ConcurrencyConflict);
            }

            return Result.Success(new Response(true, txn.Status, payment.Id, ticket.Id));
        }

        private static DateTime ParseDateExact(string? raw, DateTime fallback)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;

            // Format SePay: "2024-07-02 11:08:33"
            const string format = "yyyy-MM-dd HH:mm:ss";

            if (DateTime.TryParseExact(raw, format, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var localTime))
            {
                // SePay gửi theo giờ Việt Nam (UTC+7)
                var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime,
                    TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
                return utcTime;
            }

            return fallback;
        }
    }
}
