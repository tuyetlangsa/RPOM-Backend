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

namespace Rpom.Application.Tables.BatchCreateTables;

public static class BatchCreateTables
{
    public sealed record Command(
        int AreaId,
        string CodePrefix,
        int StartNumber,
        int Count,
        int SeatCount,
        string? Description,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(int CreatedCount, IReadOnlyList<int> CreatedIds);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.AreaId).GreaterThan(0);
            RuleFor(x => x.CodePrefix).NotEmpty().MaximumLength(40);
            RuleFor(x => x.StartNumber).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Count).InclusiveBetween(1, 100);
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
            bool areaExists = await dbContext.Areas.AnyAsync(x => x.Id == request.AreaId, ct);
            if (!areaExists)
            {
                return Result.Failure<Response>(TableErrors.AreaNotFound);
            }

            string prefix = request.CodePrefix.Trim();
            int lastNumber = request.StartNumber + request.Count - 1;
            // Padding: nếu số cuối ≥ 10 thì pad theo độ dài số cuối, để code sort gọn
            int width = lastNumber.ToString().Length;
            var codes = Enumerable.Range(request.StartNumber, request.Count)
                .Select(n => prefix + n.ToString().PadLeft(width, '0'))
                .ToList();

            // Check trùng trong cùng Area, batch trong 1 query
            List<string> existing = await dbContext.Tables
                .Where(t => t.AreaId == request.AreaId && codes.Contains(t.Code))
                .Select(t => t.Code)
                .ToListAsync(ct);
            if (existing.Count > 0)
            {
                return Result.Failure<Response>(TableErrors.CodeDuplicateInArea);
            }

            int staffId = currentStaff.StaffAccountId;
            DateTime now = clock.UtcNow;
            string? description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

            var entities = codes.Select(code => new Table
            {
                AreaId = request.AreaId,
                Code = code,
                SeatCount = request.SeatCount,
                Description = description,
                Status = TableStatus.Available,
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            }).ToList();
            dbContext.Tables.AddRange(entities);

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return Result.Failure<Response>(TableErrors.CodeDuplicateInArea);
            }

            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Table),
                EntityId = 0,
                Action = "BATCH_CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"Batch created {entities.Count} tables: {codes[0]}..{codes[^1]} (area={request.AreaId})"
            });
            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"Table.BatchCreate(count={entities.Count})", ct);

            return Result.Success(new Response(entities.Count, entities.Select(e => e.Id).ToList()));
        }
    }
}
