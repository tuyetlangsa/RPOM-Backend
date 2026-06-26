using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Payments;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Configuration;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;
using Rpom.Domain.Sales;

namespace Rpom.Application.CustomerDisplays.PollCustomerDisplayState;
public static class PollCustomerDisplayState
{
    public sealed record Command(string DeviceToken, string ClientId) : ICommand<Response>;

    public sealed record Response(
        string Mode,
        int DisplayId,
        string DisplayName,
        int PosTerminalId,
        string CounterName,
        string? IdleMediaUrl,
        QrInfo? Qr,
        PaidInfo? Paid);

    public sealed record QrInfo(
        long PaymentId,
        long Amount,
        string ReferenceCode,
        string QrImageUrl,
        string AccountNumber,
        string BankCode,
        string AccountName,
        string TicketCode,
        DateTime? ExpiresAt);

    public sealed record PaidInfo(long PaymentId, long Amount, string TicketCode, DateTime PaidAt);

    public static class Modes
    {
        public const string Idle = "IDLE";
        public const string Qr = "QR";
        public const string Paid = "PAID";
    }

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DeviceToken).NotEmpty().MaximumLength(64);
            RuleFor(x => x.ClientId).NotEmpty().MaximumLength(64);
        }
    }

    internal sealed class Handler(
        IDbContext db,
        IDateTimeProvider clock,
        IConfigValueService config,
        IQrPaymentGateway gateway,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            CustomerDisplay? display = await db.CustomerDisplays
                .FirstOrDefaultAsync(d => d.DeviceToken == request.DeviceToken && d.IsActive, ct);
            if (display is null) return Result.Failure<Response>(CustomerDisplayErrors.InvalidToken);

            if (display.ActivatedClientId is null || display.ActivatedClientId != request.ClientId.Trim())
                return Result.Failure<Response>(CustomerDisplayErrors.NotActivated);

            DateTime now = clock.UtcNow;
            display.LastSeenAt = now;
            display.UpdatedAt = now;

            string counterName = await db.PosTerminals
                .Where(t => t.Id == display.PosTerminalId).Select(t => t.Counter.Name).FirstAsync(ct);

            async Task<Result<Response>> IdleAsync(bool bump)
            {
                string idle = display.IdleMediaUrl
                              ?? await config.GetStringAsync(ConfigCodes.CustomerDisplayIdleMediaUrl, "", ct);
                await db.SaveChangesAsync(ct);
                if (bump)
                    await versionService.BumpAsync(VersionScopes.FloorPlan, "CustomerDisplay.QrExpired", ct);
                return Result.Success(new Response(
                    Modes.Idle, display.Id, display.Name, display.PosTerminalId, counterName,
                    string.IsNullOrWhiteSpace(idle) ? null : idle, null, null));
            }

            TicketPaymentDetail? pending = await db.TicketPaymentDetails
                .Where(p => p.PosTerminalId == display.PosTerminalId
                            && p.Status == TicketPaymentStatus.Pending
                            && p.PaymentMethod.Code == PaymentMethodCodes.Qr)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (pending is not null && pending.ExpiresAt is { } exp && exp < now)
            {
                pending.Status = TicketPaymentStatus.Cancelled;
                pending.ProcessedAt = now;
                pending.UpdatedAt = now;
                db.AuditLogs.Add(new AuditLog
                {
                    EntityType = nameof(Ticket),
                    EntityId = pending.TicketId,
                    Action = "PAYMENT_QR_EXPIRED",
                    ActorStaffAccountId = null,
                    ActorFullName = "SYSTEM",
                    Timestamp = now,
                    Summary = $"QR chờ #{pending.Id} ({pending.Amount:N0}đ) hết hạn — tự huỷ",
                });
                return await IdleAsync(true);
            }

            if (pending is null || !gateway.IsConfigured)
            {
                // Không còn QR chờ → kiểm tra QR vừa SUCCESS gần đây để hiện "Thanh toán thành công".
                int splashSeconds = await config.GetIntAsync(ConfigCodes.CustomerDisplayPaidSplashSeconds, 5, ct);
                if (splashSeconds > 0)
                {
                    var paid = await db.TicketPaymentDetails
                        .Where(p => p.PosTerminalId == display.PosTerminalId
                                    && p.Status == TicketPaymentStatus.Success
                                    && p.PaymentMethod.Code == PaymentMethodCodes.Qr
                                    && p.ProcessedAt != null)
                        .OrderByDescending(p => p.ProcessedAt)
                        .Select(p => new { p.Id, p.Amount, p.ProcessedAt, TicketCode = p.Ticket.Code })
                        .FirstOrDefaultAsync(ct);

                    if (paid?.ProcessedAt is { } paidAt && (now - paidAt).TotalSeconds <= splashSeconds)
                    {
                        await db.SaveChangesAsync(ct);
                        return Result.Success(new Response(
                            Modes.Paid, display.Id, display.Name, display.PosTerminalId, counterName,
                            null, null,
                            new PaidInfo(paid.Id, (long)paid.Amount, paid.TicketCode, paidAt)));
                    }
                }

                return await IdleAsync(false);
            }

            string ticketCode = await db.Tickets
                .Where(t => t.Id == pending.TicketId).Select(t => t.Code).FirstAsync(ct);

            string reference = gateway.BuildReferenceCode(pending.Id);
            QrCodeDescriptor qr = gateway.BuildQrCode(reference, pending.Amount);

            await db.SaveChangesAsync(ct);

            return Result.Success(new Response(
                Modes.Qr, display.Id, display.Name, display.PosTerminalId, counterName, null,
                new QrInfo(
                    pending.Id, (long)pending.Amount, qr.ReferenceCode, qr.QrImageUrl,
                    qr.AccountNumber, qr.BankCode, qr.AccountName, ticketCode, pending.ExpiresAt),
                null));
        }
    }
}
