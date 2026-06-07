using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.PriceVariants.Shared;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.PriceVariants.CreatePriceVariant;

public static class CreatePriceVariant
{
    public sealed record Command(
        int PriceTableId,
        string Code,
        string Name,
        string? Description,
        TimeOnly? BeginTime,
        TimeOnly? EndTime,
        int? DayMask,
        bool AppliesToAllAreas,
        IReadOnlyList<int> AreaIds,
        bool IsActive) : ICommand<Response>;

    public sealed record Response(
        int Id,
        int PriceTableId,
        string Code,
        string Name,
        string? Description,
        TimeOnly? BeginTime,
        TimeOnly? EndTime,
        int? DayMask,
        bool AppliesToAllAreas,
        IReadOnlyList<int> AreaIds,
        int Specificity,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PriceTableId).GreaterThan(0);
            RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
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
            // 1. Parent exists
            bool parentExists = await dbContext.PriceTables.AnyAsync(t => t.Id == request.PriceTableId, ct);
            if (!parentExists)
            {
                return Result.Failure<Response>(PriceVariantErrors.PriceTableNotFound);
            }

            // 2. Validate time range
            Error? timeCheck = ValidateTimeRange(request.BeginTime, request.EndTime);
            if (timeCheck is not null)
            {
                return Result.Failure<Response>(timeCheck);
            }

            // 3. Validate DayMask
            if (request.DayMask is not null && (request.DayMask < 1 || request.DayMask > 127))
            {
                return Result.Failure<Response>(PriceVariantErrors.DayMaskInvalid);
            }

            // 4. Validate scope
            Error? scopeCheck = await ValidateAreaScope(request.AppliesToAllAreas, request.AreaIds, ct);
            if (scopeCheck is not null)
            {
                return Result.Failure<Response>(scopeCheck);
            }

            // 5. Code unique within PriceTable (case-insensitive)
            string code = request.Code.Trim();
            string codeLower = code.ToLower();
            bool codeDup = await dbContext.PriceVariants
                .AnyAsync(v => v.PriceTableId == request.PriceTableId && v.Code.ToLower() == codeLower, ct);
            if (codeDup)
            {
                return Result.Failure<Response>(PriceVariantErrors.CodeDuplicate);
            }

            // 6. Overlap + spec=0 dup check
            List<OverlapChecker.VariantSnapshot> siblings = await LoadSiblings(request.PriceTableId, 0, ct);
            var draft = new OverlapChecker.VariantSnapshot(
                0,
                code,
                request.BeginTime,
                request.EndTime,
                request.DayMask,
                request.AppliesToAllAreas,
                request.AreaIds);

            if (OverlapChecker.Specificity(draft) == 0
                && siblings.Any(s => OverlapChecker.Specificity(s) == 0))
            {
                return Result.Failure<Response>(PriceVariantErrors.DefaultVariantExists);
            }

            OverlapChecker.VariantSnapshot? conflict = OverlapChecker.FindConflict(draft, siblings);
            if (conflict is not null)
            {
                return Result.Failure<Response>(PriceVariantErrors.OverlapConflict(conflict.Code));
            }

            // 7. Persist
            DateTime now = clock.UtcNow;
            int staffId = currentStaff.StaffAccountId;

            var entity = new PriceVariant
            {
                PriceTableId = request.PriceTableId,
                Code = code,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                BeginTime = request.BeginTime,
                EndTime = request.EndTime,
                DayMask = request.DayMask,
                AppliesToAllAreas = request.AppliesToAllAreas,
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.PriceVariants.Add(entity);
            await dbContext.SaveChangesAsync(ct);

            if (!request.AppliesToAllAreas)
            {
                foreach (int areaId in request.AreaIds.Distinct())
                {
                    dbContext.PriceVariantAreas.Add(new PriceVariantArea
                    {
                        PriceVariantId = entity.Id,
                        AreaId = areaId,
                        CreatedAt = now
                    });
                }
            }

            StaffAccount staff = await dbContext.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(PriceVariant),
                EntityId = entity.Id,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary =
                    $"PriceVariant created: {entity.Code} — {entity.Name} (table={entity.PriceTableId}, spec={OverlapChecker.Specificity(draft)})"
            });
            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"PriceVariant.Create(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.PriceTableId, entity.Code, entity.Name, entity.Description,
                entity.BeginTime, entity.EndTime, entity.DayMask, entity.AppliesToAllAreas,
                request.AppliesToAllAreas ? Array.Empty<int>() : request.AreaIds.Distinct().ToList(),
                OverlapChecker.Specificity(draft),
                entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }

        private static Error? ValidateTimeRange(TimeOnly? begin, TimeOnly? end)
        {
            // Cả 2 NULL = OK (whole day). Nếu 1 trong 2 set → cả 2 phải set + Begin < End.
            if (begin is null && end is null)
            {
                return null;
            }

            if (begin is null || end is null)
            {
                return PriceVariantErrors.TimeRangeInvalid;
            }

            if (begin.Value >= end.Value)
            {
                return PriceVariantErrors.TimeRangeInvalid;
            }

            return null;
        }

        private async Task<Error?> ValidateAreaScope(bool appliesToAll, IReadOnlyList<int> areaIds,
            CancellationToken ct)
        {
            if (appliesToAll)
            {
                if (areaIds.Count > 0)
                {
                    return PriceVariantErrors.AreaListMustBeEmpty;
                }

                return null;
            }

            if (areaIds.Count == 0)
            {
                return PriceVariantErrors.AreaListRequired;
            }

            var distinctIds = areaIds.Distinct().ToList();
            int foundCount = await dbContext.Areas.CountAsync(a => distinctIds.Contains(a.Id), ct);
            if (foundCount != distinctIds.Count)
            {
                return PriceVariantErrors.AreaNotFound;
            }

            return null;
        }

        private async Task<List<OverlapChecker.VariantSnapshot>> LoadSiblings(int priceTableId, int excludeId,
            CancellationToken ct)
        {
            var raw = await dbContext.PriceVariants
                .Where(v => v.PriceTableId == priceTableId && v.Id != excludeId)
                .Select(v => new
                {
                    v.Id,
                    v.Code,
                    v.BeginTime,
                    v.EndTime,
                    v.DayMask,
                    v.AppliesToAllAreas,
                    AreaIds = dbContext.PriceVariantAreas
                        .Where(a => a.PriceVariantId == v.Id)
                        .Select(a => a.AreaId).ToList()
                })
                .ToListAsync(ct);
            return raw.Select(v => new OverlapChecker.VariantSnapshot(
                v.Id, v.Code, v.BeginTime, v.EndTime, v.DayMask, v.AppliesToAllAreas, v.AreaIds)).ToList();
        }
    }
}
