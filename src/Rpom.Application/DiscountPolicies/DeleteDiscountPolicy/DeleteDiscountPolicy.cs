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
using Rpom.Domain.Operations;

namespace Rpom.Application.DiscountPolicies.DeleteDiscountPolicy;

/// <summary>
///     Hard-delete a DiscountPolicy (its Conditions cascade). Refuses if any Ticket
///     still references it (FK Restrict) — Owner should deactivate instead.
/// </summary>
public static class DeleteDiscountPolicy
{
    public sealed record Command(int Id) : ICommand;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command>
    {
        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            DiscountPolicy? policy = await db.DiscountPolicies.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
            if (policy is null)
            {
                return Result.Failure(DiscountPolicyErrors.NotFound);
            }

            bool inUse = await db.Tickets.AnyAsync(t => t.DiscountPolicyId == request.Id, ct);
            if (inUse)
            {
                return Result.Failure(DiscountPolicyErrors.InUse);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;
            string snapshotCode = policy.Code;

            db.DiscountPolicies.Remove(policy);

            StaffAccount staff = await db.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(DiscountPolicy),
                EntityId = request.Id,
                Action = "DELETE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"DiscountPolicy deleted: {snapshotCode}"
            });

            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"DiscountPolicy.Delete(id={request.Id})", ct);
            return Result.Success();
        }
    }
}
