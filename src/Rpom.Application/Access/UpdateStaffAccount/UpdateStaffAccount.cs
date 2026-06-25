using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;

namespace Rpom.Application.Access.UpdateStaffAccount;

public static class UpdateStaffAccount
{
    public sealed record Command(
        int Id,
        string FullName,
        string? Phone,
        string? Email,
        int RoleId,
        bool IsActive,
        bool IsLocked) : ICommand<Response>;

    public sealed record Response(
        int Id, string Username, string FullName, string? Phone, string? Email,
        int RoleId, string RoleCode, string RoleName, bool IsActive, bool IsLocked,
        DateTime? LastLoginAt, DateTime CreatedAt, DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Phone).MaximumLength(20);
            RuleFor(x => x.Email).MaximumLength(200);
            RuleFor(x => x.RoleId).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            StaffAccount? entity = await db.StaffAccounts
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null)
            {
                return Result.Failure<Response>(AccessErrors.StaffNotFound);
            }

            if (entity.RoleId != request.RoleId)
            {
                bool roleExists = await db.Roles.AnyAsync(r => r.Id == request.RoleId, ct);
                if (!roleExists)
                {
                    return Result.Failure<Response>(AccessErrors.RoleNotFound);
                }
            }

            DateTime now = clock.UtcNow;
            entity.FullName = request.FullName.Trim();
            entity.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            entity.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            entity.RoleId = request.RoleId;
            entity.IsActive = request.IsActive;
            entity.IsLocked = request.IsLocked;
            entity.UpdatedAt = now;

            StaffAccount actor = await db.StaffAccounts.FirstAsync(x => x.Id == currentStaff.StaffAccountId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(StaffAccount),
                EntityId = entity.Id,
                Action = "UPDATE",
                ActorStaffAccountId = actor.Id,
                ActorFullName = actor.FullName,
                Timestamp = now,
                Summary = $"Staff account updated: {entity.Username}"
            });

            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Access, $"StaffAccount.Update(id={entity.Id})", ct);

            Role role = await db.Roles.FirstAsync(r => r.Id == entity.RoleId, ct);
            return Result.Success(new Response(
                entity.Id, entity.Username, entity.FullName, entity.Phone, entity.Email,
                entity.RoleId, role.Code, role.Name, entity.IsActive, entity.IsLocked,
                entity.LastLoginAt, entity.CreatedAt, entity.UpdatedAt));
        }
    }
}
