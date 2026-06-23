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
using Rpom.Domain.Restaurant;

namespace Rpom.Application.PosTerminals.RegisterPosTerminal;
public static class RegisterPosTerminal
{
    public sealed record Command(int CounterId, string Name) : ICommand<Response>;

    public sealed record Response(int Id, int CounterId, string Name, string DeviceToken);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.CounterId).GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        }
    }

    internal sealed class Handler(IDbContext db, ICurrentStaff currentStaff, IDateTimeProvider clock)
        : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            bool counterOk = await db.Counters.AnyAsync(c => c.Id == request.CounterId && c.IsActive, ct);
            if (!counterOk) return Result.Failure<Response>(CounterErrors.NotFound);

            DateTime now = clock.UtcNow;
            var terminal = new PosTerminal
            {
                CounterId = request.CounterId,
                Name = request.Name.Trim(),
                DeviceToken = Guid.NewGuid().ToString("N"),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.PosTerminals.Add(terminal);

            int staffId = currentStaff.StaffAccountId;
            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(PosTerminal),
                EntityId = 0,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Đăng ký máy POS '{terminal.Name}' tại quầy {terminal.CounterId}",
            });

            await db.SaveChangesAsync(ct);
            return Result.Success(new Response(terminal.Id, terminal.CounterId, terminal.Name, terminal.DeviceToken));
        }
    }
}
