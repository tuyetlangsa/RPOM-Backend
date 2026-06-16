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

namespace Rpom.Application.KitchenStations.CreateKitchenStation;

public static class CreateKitchenStation
{
    public sealed record Command(
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
            string code = request.Code.Trim();
            string codeLower = code.ToLower();

            // Case-insensitive duplicate check.
            bool duplicate = await dbContext.KitchenStations
                .AnyAsync(x => x.Code.ToLower() == codeLower, ct);
            if (duplicate)
            {
                return Result.Failure<Response>(KitchenStationErrors.CodeDuplicate);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            var entity = new KitchenStation
            {
                Code = code,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.KitchenStations.Add(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Race condition safety net — DB unique index caught what pre-check missed.
                return Result.Failure<Response>(KitchenStationErrors.CodeDuplicate);
            }

            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(KitchenStation),
                EntityId = entity.Id,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"KitchenStation created: {entity.Code} — {entity.Name}"
            });
            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"KitchenStation.Create(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.Code, entity.Name, entity.Description,
                entity.DisplayOrder, entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }
    }
}
