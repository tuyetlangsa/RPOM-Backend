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

namespace Rpom.Application.CustomerDisplays.UpdateCustomerDisplay;
public static class UpdateCustomerDisplay
{
    public sealed record Command(int Id, string Name, string? IdleMediaUrl) : ICommand<Response>;

    public sealed record Response(int Id, string Name, string? IdleMediaUrl);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.IdleMediaUrl).MaximumLength(500);
        }
    }

    internal sealed class Handler(IDbContext db, ICurrentStaff currentStaff, IDateTimeProvider clock)
        : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            CustomerDisplay? display = await db.CustomerDisplays.FirstOrDefaultAsync(d => d.Id == request.Id, ct);
            if (display is null) return Result.Failure<Response>(CustomerDisplayErrors.NotFound);

            DateTime now = clock.UtcNow;
            display.Name = request.Name.Trim();
            display.IdleMediaUrl = string.IsNullOrWhiteSpace(request.IdleMediaUrl) ? null : request.IdleMediaUrl.Trim();
            display.UpdatedAt = now;

            int staffId = currentStaff.StaffAccountId;
            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(CustomerDisplay),
                EntityId = display.Id,
                Action = "UPDATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Cập nhật màn hình khách '{display.Name}'",
            });

            await db.SaveChangesAsync(ct);
            return Result.Success(new Response(display.Id, display.Name, display.IdleMediaUrl));
        }
    }
}
