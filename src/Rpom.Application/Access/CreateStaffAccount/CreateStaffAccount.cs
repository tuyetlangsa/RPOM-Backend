using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Authentication;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;

namespace Rpom.Application.Access.CreateStaffAccount;

public static class CreateStaffAccount
{
    public sealed record Command(
        string Username,
        string Password,
        string FullName,
        string? Phone,
        string? Email,
        int RoleId) : ICommand<Response>;

    public sealed record Response(int Id, string Username, string FullName, int RoleId);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
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
        IVersionService versionService,
        IPasswordHasher passwordHasher) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            string username = request.Username.Trim();
            string usernameLower = username.ToLower();
            bool dup = await db.StaffAccounts.AnyAsync(x => x.Username.ToLower() == usernameLower, ct);
            if (dup)
            {
                return Result.Failure<Response>(AccessErrors.UsernameDuplicate);
            }

            bool roleExists = await db.Roles.AnyAsync(r => r.Id == request.RoleId, ct);
            if (!roleExists)
            {
                return Result.Failure<Response>(AccessErrors.RoleNotFound);
            }

            DateTime now = clock.UtcNow;
            var entity = new StaffAccount
            {
                Username = username,
                PasswordHash = passwordHasher.Hash(request.Password),
                FullName = request.FullName.Trim(),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                RoleId = request.RoleId,
                IsActive = true,
                IsLocked = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.StaffAccounts.Add(entity);
            await db.SaveChangesAsync(ct); // first save → entity.Id assigned

            // AuditLog is append-only (INSERT only) — insert AFTER we know entity.Id.
            // Both saves run inside the handler's transaction (TransactionPipelineBehavior), staying atomic.
            // Same two-save pattern as AccessSeeder.
            StaffAccount actor = await db.StaffAccounts.FirstAsync(x => x.Id == currentStaff.StaffAccountId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(StaffAccount),
                EntityId = entity.Id,
                Action = "CREATE",
                ActorStaffAccountId = actor.Id,
                ActorFullName = actor.FullName,
                Timestamp = now,
                Summary = $"Staff account created: {username}"
            });
            await db.SaveChangesAsync(ct);

            await versionService.BumpAsync(VersionScopes.Access, $"StaffAccount.Create(id={entity.Id})", ct);

            return Result.Success(new Response(entity.Id, entity.Username, entity.FullName, entity.RoleId));
        }
    }
}
