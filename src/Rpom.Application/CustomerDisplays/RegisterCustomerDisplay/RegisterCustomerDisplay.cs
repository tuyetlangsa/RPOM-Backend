using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.CustomerDisplays.RegisterCustomerDisplay;
public static class RegisterCustomerDisplay
{
    public sealed record Command(int PosTerminalId, string Name, string? IdleMediaUrl) : ICommand<Response>;

    public sealed record Response(int Id, int PosTerminalId, string Name, string DeviceToken, string? IdleMediaUrl);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PosTerminalId).GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.IdleMediaUrl).MaximumLength(500);
        }
    }

    internal sealed class Handler(IDbContext db, ICurrentStaff currentStaff, IDateTimeProvider clock)
        : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            bool posTerminal = await db.PosTerminals
                .AnyAsync(t => t.Id == request.PosTerminalId && t.IsActive, ct);
            if (!posTerminal) return Result.Failure<Response>(PosTerminalErrors.NotFound);

            bool alreadyLinked = await db.CustomerDisplays
                .AnyAsync(d => d.PosTerminalId == request.PosTerminalId && d.IsActive, ct);
            if (alreadyLinked) return Result.Failure<Response>(CustomerDisplayErrors.TerminalAlreadyLinked);

            DateTime now = clock.UtcNow;
            var display = new CustomerDisplay
            {
                PosTerminalId = request.PosTerminalId,
                Name = request.Name.Trim(),
                DeviceToken = Guid.NewGuid().ToString("N"),
                IdleMediaUrl = string.IsNullOrWhiteSpace(request.IdleMediaUrl) ? null : request.IdleMediaUrl.Trim(),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.CustomerDisplays.Add(display);

            int staffId = currentStaff.StaffAccountId;
            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(CustomerDisplay),
                EntityId = 0,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Đăng ký màn hình khách '{display.Name}' gắn POS #{display.PosTerminalId}",
            });

            await db.SaveChangesAsync(ct);
            return Result.Success(new Response(
                display.Id, display.PosTerminalId, display.Name, display.DeviceToken, display.IdleMediaUrl));
        }
    }
}
