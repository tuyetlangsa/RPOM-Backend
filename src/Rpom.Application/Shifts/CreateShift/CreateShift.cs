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

namespace Rpom.Application.Shifts.CreateShift;

public static class CreateShift
{
    public sealed record Command(
        string Code,
        string Name,
        TimeOnly BeginTime,
        TimeOnly EndTime,
        bool IsNextDay,
        string? Note,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        TimeOnly BeginTime,
        TimeOnly EndTime,
        bool IsNextDay,
        string? Note,
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
            RuleFor(x => x.Note).MaximumLength(500);
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
            bool duplicate = await dbContext.Shifts
                .AnyAsync(x => x.Code.ToLower() == codeLower, ct);
            if (duplicate)
            {
                return Result.Failure<Response>(ShiftErrors.CodeDuplicate);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;

            var entity = new Shift
            {
                Code = code,
                Name = request.Name.Trim(),
                BeginTime = request.BeginTime,
                EndTime = request.EndTime,
                IsNextDay = request.IsNextDay,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.Shifts.Add(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Race condition safety net — DB unique index caught what pre-check missed.
                return Result.Failure<Response>(ShiftErrors.CodeDuplicate);
            }

            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Shift),
                EntityId = entity.Id,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Shift created: {entity.Code} — {entity.Name}"
            });
            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Config, $"Shift.Create(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.Code, entity.Name,
                entity.BeginTime, entity.EndTime, entity.IsNextDay, entity.Note,
                entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }
    }
}
