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

namespace Rpom.Application.Access.SetStaffPermissions;

public static class SetStaffPermissions
{
    public sealed record Command(
        int StaffAccountId,
        IReadOnlyList<string> PermissionCodes) : ICommand<Response>;

    public sealed record Response(int StaffAccountId, IReadOnlyList<string> GrantedPermissionCodes);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.StaffAccountId).GreaterThan(0);
            RuleFor(x => x.PermissionCodes).NotNull();
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
            StaffAccount? target = await db.StaffAccounts
                .FirstOrDefaultAsync(x => x.Id == request.StaffAccountId, ct);
            if (target is null)
            {
                return Result.Failure<Response>(AccessErrors.StaffNotFound);
            }

            var requestedCodes = request.PermissionCodes.Distinct().ToList();
            Dictionary<string, int> permIdByCode = await db.Permissions
                .Where(p => requestedCodes.Contains(p.Code))
                .ToDictionaryAsync(p => p.Code, p => p.Id, ct);

            if (permIdByCode.Count != requestedCodes.Count)
            {
                return Result.Failure<Response>(AccessErrors.UnknownPermissionCode);
            }

            var requestedIds = permIdByCode.Values.ToHashSet();

            List<StaffAccountPermission> current = await db.StaffAccountPermissions
                .Where(x => x.StaffAccountId == request.StaffAccountId)
                .ToListAsync(ct);
            var currentIds = current.Select(x => x.PermissionId).ToHashSet();

            DateTime now = clock.UtcNow;

            // Remove grants no longer requested.
            foreach (StaffAccountPermission row in current.Where(x => !requestedIds.Contains(x.PermissionId)))
            {
                db.StaffAccountPermissions.Remove(row);
            }

            // Add newly requested grants.
            foreach (int permId in requestedIds.Where(id => !currentIds.Contains(id)))
            {
                db.StaffAccountPermissions.Add(new StaffAccountPermission
                {
                    StaffAccountId = request.StaffAccountId,
                    PermissionId = permId,
                    CreatedAt = now
                });
            }

            StaffAccount actor = await db.StaffAccounts.FirstAsync(x => x.Id == currentStaff.StaffAccountId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(StaffAccount),
                EntityId = request.StaffAccountId,
                Action = "UPDATE",
                ActorStaffAccountId = actor.Id,
                ActorFullName = actor.FullName,
                Timestamp = now,
                Summary = $"Permissions set ({requestedCodes.Count}) for account #{request.StaffAccountId}"
            });

            await db.SaveChangesAsync(ct);

            await versionService.BumpAsync(
                VersionScopes.Access,
                $"StaffAccountPermission.Set(staffId={request.StaffAccountId})",
                ct);

            return Result.Success(new Response(request.StaffAccountId, requestedCodes));
        }
    }
}
