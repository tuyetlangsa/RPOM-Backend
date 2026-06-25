using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Authentication;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;

namespace Rpom.Application.Access.ResetPassword;

public static class ResetPassword
{
    public sealed record Command(int StaffAccountId, string NewPassword) : ICommand;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.StaffAccountId).GreaterThan(0);
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IPasswordHasher passwordHasher) : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            StaffAccount? entity = await db.StaffAccounts
                .FirstOrDefaultAsync(x => x.Id == request.StaffAccountId, ct);
            if (entity is null)
            {
                return Result.Failure(AccessErrors.StaffNotFound);
            }

            DateTime now = clock.UtcNow;
            entity.PasswordHash = passwordHasher.Hash(request.NewPassword);
            entity.UpdatedAt = now;

            StaffAccount actor = await db.StaffAccounts.FirstAsync(x => x.Id == currentStaff.StaffAccountId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(StaffAccount),
                EntityId = entity.Id,
                Action = "RESET_PASSWORD",
                ActorStaffAccountId = actor.Id,
                ActorFullName = actor.FullName,
                Timestamp = now,
                Summary = $"Password reset for: {entity.Username}"
            });

            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
    }
}
