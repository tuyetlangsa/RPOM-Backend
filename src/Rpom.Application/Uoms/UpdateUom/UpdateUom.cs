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

namespace Rpom.Application.Uoms.UpdateUom;

/// <summary>
///     BR-2: Code is editable even when in use — transactional snapshot tables
///     (CartItem/OrderItem/TicketItemSum) already captured UomCode at write time,
///     so renaming the master record never breaks historical accuracy.
///     BR-3: IsActive=false is allowed regardless of FK references (soft retire).
/// </summary>
public static class UpdateUom
{
    public sealed record Command(
        int Id,
        string Code,
        string Name,
        string? Description,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
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
            Uom? entity = await dbContext.Uoms.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null)
            {
                return Result.Failure<Response>(UomErrors.NotFound);
            }

            string code = request.Code.Trim();
            string codeLower = code.ToLower();

            // Allow same code as current; reject if any OTHER Uom uses it (case-insensitive).
            bool duplicate = await dbContext.Uoms
                .AnyAsync(x => x.Id != request.Id && x.Code.ToLower() == codeLower, ct);
            if (duplicate)
            {
                return Result.Failure<Response>(UomErrors.CodeDuplicate);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;
            string summary = BuildSummary(entity, request, code);

            entity.Code = code;
            entity.Name = request.Name.Trim();
            entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = now;

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Uom),
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
                return Result.Failure<Response>(UomErrors.CodeDuplicate);
            }

            await versionService.BumpAsync(VersionScopes.Menu, $"Uom.Update(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.Code, entity.Name, entity.Description,
                entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }

        private static string BuildSummary(Uom before, Command after, string normalizedCode)
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

            if (before.IsActive != after.IsActive)
            {
                diffs.Add($"isActive: {before.IsActive} → {after.IsActive}");
            }

            return diffs.Count == 0 ? "Uom updated (no changes)" : string.Join("; ", diffs);
        }
    }
}
