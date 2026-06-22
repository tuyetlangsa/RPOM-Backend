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

namespace Rpom.Application.CustomerDisplays.InactivateCustomerDisplay;
public static class InactivateCustomerDisplay
{
    public sealed record Command(int Id) : ICommand<Response>;

    public sealed record Response(int Id, bool IsActive);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.Id).GreaterThan(0);
    }

    internal sealed class Handler(IDbContext db, ICurrentStaff currentStaff, IDateTimeProvider clock)
        : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            CustomerDisplay? display = await db.CustomerDisplays.FirstOrDefaultAsync(d => d.Id == request.Id, ct);
            if (display is null) return Result.Failure<Response>(CustomerDisplayErrors.NotFound);

            DateTime now = clock.UtcNow;
            display.IsActive = false;
            display.UpdatedAt = now;

            int staffId = currentStaff.StaffAccountId;
            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(CustomerDisplay),
                EntityId = display.Id,
                Action = "INACTIVATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Vô hiệu màn hình khách '{display.Name}'",
            });

            await db.SaveChangesAsync(ct);
            return Result.Success(new Response(display.Id, display.IsActive));
        }
    }
}
