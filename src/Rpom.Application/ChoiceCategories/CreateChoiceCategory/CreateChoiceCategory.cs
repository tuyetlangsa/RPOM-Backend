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
using Rpom.Domain.Menu;

namespace Rpom.Application.ChoiceCategories.CreateChoiceCategory;

public static class CreateChoiceCategory
{
    public sealed record Command(
        string Name,
        string? Note,
        short MinChoice,
        short? MaxChoice,
        short DisplayOrder,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        string Name,
        string? Note,
        short MinChoice,
        short? MaxChoice,
        short DisplayOrder,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Note).MaximumLength(200);
            RuleFor(x => x.MinChoice).GreaterThanOrEqualTo((short)0);
            RuleFor(x => x.MaxChoice)
                .GreaterThanOrEqualTo(x => x.MinChoice)
                .When(x => x.MaxChoice.HasValue);
            RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo((short)0);
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
            string name = request.Name.Trim();
            string nameLower = name.ToLower();
            bool duplicate = await db.ChoiceCategories.AnyAsync(c => c.Name.ToLower() == nameLower, ct);
            if (duplicate)
            {
                return Result.Failure<Response>(ChoiceCategoryErrors.NameDuplicate);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            var entity = new ChoiceCategory
            {
                Name = name,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                MinChoice = request.MinChoice,
                MaxChoice = request.MaxChoice,
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.ChoiceCategories.Add(entity);

            StaffAccount staff = await db.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return Result.Failure<Response>(ChoiceCategoryErrors.NameDuplicate);
            }

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(ChoiceCategory),
                EntityId = entity.Id,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"ChoiceCategory created: {entity.Name}"
            });
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"ChoiceCategory.Create(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.Name, entity.Note, entity.MinChoice, entity.MaxChoice,
                entity.DisplayOrder, entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }
    }
}
