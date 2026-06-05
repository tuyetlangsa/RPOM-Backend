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

namespace Rpom.Application.Tables.CreateTable;

public static class CreateTable
{
    public sealed record Command(
        int AreaId,
        string Code,
        int SeatCount,
        string? Description,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        int AreaId,
        string Code,
        int SeatCount,
        string? Description,
        string Status,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.AreaId).GreaterThan(0);
            RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
            RuleFor(x => x.SeatCount).GreaterThan(0);
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
            var areaExists = await dbContext.Areas.AnyAsync(x => x.Id == request.AreaId, ct);
            if (!areaExists) return Result.Failure<Response>(TableErrors.AreaNotFound);

            var code = request.Code.Trim();
            var dup = await dbContext.Tables.AnyAsync(
                x => x.AreaId == request.AreaId && x.Code == code, ct);
            if (dup) return Result.Failure<Response>(TableErrors.CodeDuplicateInArea);

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;

            var entity = new Table
            {
                AreaId = request.AreaId,
                Code = code,
                SeatCount = request.SeatCount,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Status = TableStatus.Available,
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now,
            };
            dbContext.Tables.Add(entity);

            var staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            await dbContext.SaveChangesAsync(ct);

            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Table),
                EntityId = entity.Id,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Table created: {entity.Code} (area={entity.AreaId})",
            });
            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Table.Create(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.AreaId, entity.Code, entity.SeatCount, entity.Description,
                entity.Status, entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }
    }
}
