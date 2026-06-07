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
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Areas.UpdateArea;

public static class UpdateArea
{
    public sealed record Command(
        int Id,
        int CounterId,
        string Name,
        string? Description,
        short DisplayOrder,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        int CounterId,
        string Name,
        string? Description,
        short DisplayOrder,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.CounterId).GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(500);
            RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo((short)0);
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            Area? entity = await dbContext.Areas.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null)
            {
                return Result.Failure<Response>(AreaErrors.NotFound);
            }

            if (entity.CounterId != request.CounterId)
            {
                bool counterExists = await dbContext.Counters.AnyAsync(x => x.Id == request.CounterId, ct);
                if (!counterExists)
                {
                    return Result.Failure<Response>(AreaErrors.CounterNotFound);
                }
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;
            string summary = BuildSummary(entity, request);

            entity.CounterId = request.CounterId;
            entity.Name = request.Name.Trim();
            entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            entity.DisplayOrder = request.DisplayOrder;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = now;

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Area),
                EntityId = entity.Id,
                Action = "UPDATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = summary
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Area.Update(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.CounterId, entity.Name, entity.Description,
                entity.DisplayOrder, entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }

        private static string BuildSummary(Area before, Command after)
        {
            var diffs = new List<string>();
            if (before.CounterId != after.CounterId)
            {
                diffs.Add($"counterId: {before.CounterId} → {after.CounterId}");
            }

            if (before.Name != after.Name.Trim())
            {
                diffs.Add($"name: '{before.Name}' → '{after.Name.Trim()}'");
            }

            if ((before.Description ?? "") != (after.Description?.Trim() ?? ""))
            {
                diffs.Add("description changed");
            }

            if (before.DisplayOrder != after.DisplayOrder)
            {
                diffs.Add($"displayOrder: {before.DisplayOrder} → {after.DisplayOrder}");
            }

            if (before.IsActive != after.IsActive)
            {
                diffs.Add($"isActive: {before.IsActive} → {after.IsActive}");
            }

            return diffs.Count == 0 ? "Area updated (no changes)" : string.Join("; ", diffs);
        }
    }
}
