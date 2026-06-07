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

namespace Rpom.Application.PriceTables.CreatePriceTable;

public static class CreatePriceTable
{
    public sealed record Command(
        string Code,
        string Name,
        string? Description,
        DateOnly? BeginDate,
        DateOnly? EndDate,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
        DateOnly? BeginDate,
        DateOnly? EndDate,
        bool IsActive,
        int VariantCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Description).MaximumLength(500);
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
            if (request.BeginDate.HasValue && request.EndDate.HasValue
                                           && request.BeginDate.Value > request.EndDate.Value)
            {
                return Result.Failure<Response>(PriceTableErrors.DateRangeInvalid);
            }

            string code = request.Code.Trim();
            string codeLower = code.ToLower();
            bool duplicate = await dbContext.PriceTables
                .AnyAsync(x => x.Code.ToLower() == codeLower, ct);
            if (duplicate)
            {
                return Result.Failure<Response>(PriceTableErrors.CodeDuplicate);
            }

            DateTime now = clock.UtcNow;
            int staffId = currentStaff.StaffAccountId;

            var entity = new PriceTable
            {
                Code = code,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                BeginDate = request.BeginDate,
                EndDate = request.EndDate,
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.PriceTables.Add(entity);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(PriceTable),
                EntityId = entity.Id,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"PriceTable created: {entity.Code} — {entity.Name}"
            });
            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"PriceTable.Create(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.Code, entity.Name, entity.Description,
                entity.BeginDate, entity.EndDate, entity.IsActive,
                0, entity.CreatedAt, entity.UpdatedAt));
        }
    }
}
