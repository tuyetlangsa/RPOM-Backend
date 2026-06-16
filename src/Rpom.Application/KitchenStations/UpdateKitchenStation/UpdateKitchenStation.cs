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

namespace Rpom.Application.KitchenStations.UpdateKitchenStation;

public static class UpdateKitchenStation
{
    public sealed record Command(
        int Id,
        string Code,
        string Name,
        string? Description,
        short DisplayOrder,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        string Code,
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
            RuleFor(x => x.Code)
                .NotEmpty()
                .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Code must not be whitespace only.")
                .MaximumLength(20);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Description).MaximumLength(200);
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
            KitchenStation? entity = await dbContext.KitchenStations.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null)
            {
                return Result.Failure<Response>(KitchenStationErrors.NotFound);
            }

            string code = request.Code.Trim();
            string codeLower = code.ToLower();

            // Allow same code as current; reject if any OTHER KitchenStation uses it (case-insensitive).
            bool duplicate = await dbContext.KitchenStations
                .AnyAsync(x => x.Id != request.Id && x.Code.ToLower() == codeLower, ct);
            if (duplicate)
            {
                return Result.Failure<Response>(KitchenStationErrors.CodeDuplicate);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;
            string summary = BuildSummary(entity, request, code);

            entity.Code = code;
            entity.Name = request.Name.Trim();
            entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            entity.DisplayOrder = request.DisplayOrder;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = now;

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(KitchenStation),
                EntityId = entity.Id,
                Action = "UPDATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = summary
            });

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return Result.Failure<Response>(KitchenStationErrors.CodeDuplicate);
            }

            await versionService.BumpAsync(VersionScopes.Menu, $"KitchenStation.Update(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.Code, entity.Name, entity.Description,
                entity.DisplayOrder, entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }

        private static string BuildSummary(KitchenStation before, Command after, string normalizedCode)
        {
            var diffs = new List<string>();
            if (before.Code != normalizedCode)
            {
                diffs.Add($"code: '{before.Code}' → '{normalizedCode}'");
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

            return diffs.Count == 0 ? "KitchenStation updated (no changes)" : string.Join("; ", diffs);
        }
    }
}
