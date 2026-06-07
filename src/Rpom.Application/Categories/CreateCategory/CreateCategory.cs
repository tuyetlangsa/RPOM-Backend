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

namespace Rpom.Application.Categories.CreateCategory;

public static class CreateCategory
{
    public sealed record Command(
        string Code,
        string Name,
        string? Description,
        int? ParentId,
        short DisplayOrder,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
        int? ParentId,
        string Path,
        short Level,
        short DisplayOrder,
        bool IsActive,
        int ItemCount,
        int ChildCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
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
            string code = request.Code.Trim();
            string codeLower = code.ToLower();
            bool duplicate = await dbContext.Categories
                .AnyAsync(x => x.Code.ToLower() == codeLower, ct);
            if (duplicate)
            {
                return Result.Failure<Response>(CategoryErrors.CodeDuplicate);
            }

            Category? parent = null;
            if (request.ParentId.HasValue)
            {
                parent = await dbContext.Categories
                    .FirstOrDefaultAsync(x => x.Id == request.ParentId.Value, ct);
                if (parent is null)
                {
                    return Result.Failure<Response>(CategoryErrors.ParentNotFound);
                }
            }

            DateTime now = clock.UtcNow;
            int staffId = currentStaff.StaffAccountId;

            var entity = new Category
            {
                Code = code,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                ParentId = request.ParentId,
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now,
                Path = "", // set after save (need Id)
                Level = CategoryTreeHelpers.ComputeLevel(parent)
            };
            dbContext.Categories.Add(entity);
            await dbContext.SaveChangesAsync(ct);

            entity.Path = CategoryTreeHelpers.ComputePath(parent, entity.Id);
            await dbContext.SaveChangesAsync(ct);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Category),
                EntityId = entity.Id,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary =
                    $"Category created: {entity.Code} — {entity.Name} (parent={entity.ParentId?.ToString() ?? "root"})"
            });
            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"Category.Create(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.Code, entity.Name, entity.Description, entity.ParentId,
                entity.Path, entity.Level, entity.DisplayOrder, entity.IsActive,
                0, 0, entity.CreatedAt, entity.UpdatedAt));
        }
    }
}
