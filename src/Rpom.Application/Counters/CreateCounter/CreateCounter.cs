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

namespace Rpom.Application.Counters.CreateCounter;

public static class CreateCounter
{
    public sealed record Command(
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
            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;

            var entity = new Counter
            {
                Name = request.Name.Trim(),
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now,
            };
            dbContext.Counters.Add(entity);

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            await dbContext.SaveChangesAsync(ct);

            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Counter),
                EntityId = entity.Id,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Counter created: {entity.Name}",
            });
            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Counter.Create(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.Name, entity.Note, entity.DisplayOrder,
                entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }
    }
}
