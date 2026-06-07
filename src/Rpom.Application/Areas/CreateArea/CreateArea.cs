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

namespace Rpom.Application.Areas.CreateArea;

public static class CreateArea
{
    public sealed record Command(
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
            bool counterExists = await dbContext.Counters.AnyAsync(x => x.Id == request.CounterId, ct);
            if (!counterExists)
            {
                return Result.Failure<Response>(AreaErrors.CounterNotFound);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            var entity = new Area
            {
                CounterId = request.CounterId,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.Areas.Add(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            await dbContext.SaveChangesAsync(ct);

            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Area),
                EntityId = entity.Id,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Area created: {entity.Name} (counter={entity.CounterId})"
            });
            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Area.Create(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.CounterId, entity.Name, entity.Description,
                entity.DisplayOrder, entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }
    }
}
