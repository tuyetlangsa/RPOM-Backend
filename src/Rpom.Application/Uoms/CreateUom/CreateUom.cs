using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.Uoms.CreateUom;

public static class CreateUom
{
    public sealed record Command(
        string Code,
        string Name,
        string? Description,
        bool IsActive) : ICommand<UomItem>;

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
        IDateTimeProvider clock) : ICommandHandler<Command, UomItem>
    {
        public async Task<Result<UomItem>> Handle(Command request, CancellationToken ct)
        {
            var code = request.Code.Trim();
            var codeLower = code.ToLower();

            // Case-insensitive duplicate check (BR-1, BR-6).
            var duplicate = await dbContext.Uoms
                .AnyAsync(x => x.Code.ToLower() == codeLower, ct);
            if (duplicate) return Result.Failure<UomItem>(UomErrors.CodeDuplicate);

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;

            var entity = new Uom
            {
                Code = code,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now,
            };
            dbContext.Uoms.Add(entity);

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Race condition safety net — DB unique index caught what pre-check missed.
                return Result.Failure<UomItem>(UomErrors.CodeDuplicate);
            }

            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Uom),
                EntityId = entity.Id,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Uom created: {entity.Code} — {entity.Name}",
            });
            await dbContext.SaveChangesAsync(ct);

            return Result.Success(new UomItem(
                entity.Id, entity.Code, entity.Name, entity.Description,
                entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }
    }
}
