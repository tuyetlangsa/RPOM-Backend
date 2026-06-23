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

namespace Rpom.Application.PosTerminals.UpdatePosTerminal;
public static class UpdatePosTerminal
{
    public sealed record Command(int Id, string Name, int CounterId) : ICommand<Response>;

    public sealed record Response(int Id, string Name, int CounterId);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.CounterId).GreaterThan(0);
        }
    }

    internal sealed class Handler(IDbContext db, ICurrentStaff currentStaff, IDateTimeProvider clock)
        : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            PosTerminal? terminal = await db.PosTerminals.FirstOrDefaultAsync(t => t.Id == request.Id, ct);
            if (terminal is null) return Result.Failure<Response>(PosTerminalErrors.NotFound);

            if (request.CounterId != terminal.CounterId)
            {
                bool counterOk = await db.Counters.AnyAsync(c => c.Id == request.CounterId && c.IsActive, ct);
                if (!counterOk) return Result.Failure<Response>(CounterErrors.NotFound);
            }

            DateTime now = clock.UtcNow;
            int oldCounterId = terminal.CounterId;
            terminal.Name = request.Name.Trim();
            terminal.CounterId = request.CounterId;
            terminal.UpdatedAt = now;

            int staffId = currentStaff.StaffAccountId;
            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(PosTerminal),
                EntityId = terminal.Id,
                Action = "UPDATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Cập nhật máy POS '{terminal.Name}'"
                          + (request.CounterId != oldCounterId ? $" | chuyển quầy {oldCounterId}→{request.CounterId}" : ""),
            });

            await db.SaveChangesAsync(ct);
            return Result.Success(new Response(terminal.Id, terminal.Name, terminal.CounterId));
        }
    }
}
