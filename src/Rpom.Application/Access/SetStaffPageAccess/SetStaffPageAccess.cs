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

namespace Rpom.Application.Access.SetStaffPageAccess;

public static class SetStaffPageAccess
{
    public sealed record Command(
        int StaffAccountId,
        IReadOnlyList<string> PageCodes) : ICommand<Response>;

    public sealed record Response(int StaffAccountId, IReadOnlyList<string> GrantedPageCodes);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.StaffAccountId).GreaterThan(0);
            RuleFor(x => x.PageCodes).NotNull();
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

            // Distinct requested codes → resolve to page ids; any unknown code fails the whole op.
            var requestedCodes = request.PageCodes.Distinct().ToList();
            Dictionary<string, int> pageIdByCode = await db.Pages
                .Where(p => requestedCodes.Contains(p.Code))
                .ToDictionaryAsync(p => p.Code, p => p.Id, ct);

            if (pageIdByCode.Count != requestedCodes.Count)
            {
                return Result.Failure<Response>(AccessErrors.UnknownPageCode);
            }

            var requestedIds = pageIdByCode.Values.ToHashSet();

            List<StaffAccountPageAccess> current = await db.StaffAccountPageAccesses
                .Where(x => x.StaffAccountId == request.StaffAccountId)
                .ToListAsync(ct);
            var currentIds = current.Select(x => x.PageId).ToHashSet();

            DateTime now = clock.UtcNow;

            // Remove grants no longer requested.
            foreach (StaffAccountPageAccess row in current.Where(x => !requestedIds.Contains(x.PageId)))
            {
                db.StaffAccountPageAccesses.Remove(row);
            }

            // Add newly requested grants.
            foreach (int pageId in requestedIds.Where(id => !currentIds.Contains(id)))
            {
                db.StaffAccountPageAccesses.Add(new StaffAccountPageAccess
                {
                    StaffAccountId = request.StaffAccountId,
                    PageId = pageId,
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
                Summary = $"Page access set ({requestedCodes.Count} pages) for account #{request.StaffAccountId}"
            });

            await db.SaveChangesAsync(ct);

            await versionService.BumpAsync(
                VersionScopes.Access,
                $"StaffAccountPageAccess.Set(staffId={request.StaffAccountId})",
                ct);

            return Result.Success(new Response(request.StaffAccountId, requestedCodes));
        }
    }
}
