using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Counters.UpdateCounter;

public static class UpdateCounter
{
    public sealed record Command(
        int Id,
        string Name,
        string? Note,
        short DisplayOrder,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        string Name,
        string? Note,
        short DisplayOrder,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Note).MaximumLength(500);
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
            var entity = await dbContext.Counters.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null) return Result.Failure<Response>(CounterErrors.NotFound);

            var name = request.Name.Trim();
            var nameLower = name.ToLower();

            var duplicate = await dbContext.Counters
                .AnyAsync(x => x.Id != request.Id && x.Name.ToLower() == nameLower, ct);
            if (duplicate) return Result.Failure<Response>(CounterErrors.NameDuplicate);

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;
            var summary = BuildSummary(entity, request);

            entity.Name = name;
            entity.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            entity.DisplayOrder = request.DisplayOrder;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = now;

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Counter),
                EntityId = entity.Id,
                Action = "UPDATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = summary,
            });

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return Result.Failure<Response>(CounterErrors.NameDuplicate);
            }
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Counter.Update(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.Name, entity.Note, entity.DisplayOrder,
                entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }

        private static string BuildSummary(Counter before, Command after)
        {
            var diffs = new List<string>();
            if (before.Name != after.Name.Trim())
                diffs.Add($"name: '{before.Name}' → '{after.Name.Trim()}'");
            if ((before.Note ?? "") != (after.Note?.Trim() ?? ""))
                diffs.Add("note changed");
            if (before.DisplayOrder != after.DisplayOrder)
                diffs.Add($"displayOrder: {before.DisplayOrder} → {after.DisplayOrder}");
            if (before.IsActive != after.IsActive)
                diffs.Add($"isActive: {before.IsActive} → {after.IsActive}");
            return diffs.Count == 0 ? "Counter updated (no changes)" : string.Join("; ", diffs);
        }
    }
}
