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

namespace Rpom.Application.PriceVariants.UpdatePriceVariant;

public static class UpdatePriceVariant
{
    public sealed record Command(
        int Id,
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
            RuleFor(x => x.Id).GreaterThan(0);
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
            PriceVariant? entity = await dbContext.PriceVariants.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (entity is null)
            {
                return Result.Failure<Response>(PriceVariantErrors.NotFound);
            }

            // Validate time / day / scope
            Error? timeCheck = ValidateTimeRange(request.BeginTime, request.EndTime);
            if (timeCheck is not null)
            {
                return Result.Failure<Response>(timeCheck);
            }

            if (request.DayMask is not null && (request.DayMask < 1 || request.DayMask > 127))
            {
                return Result.Failure<Response>(PriceVariantErrors.DayMaskInvalid);
            }

            Error? scopeCheck = await ValidateAreaScope(request.AppliesToAllAreas, request.AreaIds, ct);
            if (scopeCheck is not null)
            {
                return Result.Failure<Response>(scopeCheck);
            }

            string code = request.Code.Trim();
            string codeLower = code.ToLower();
            bool codeDup = await dbContext.PriceVariants
                .AnyAsync(v => v.PriceTableId == entity.PriceTableId
                               && v.Id != request.Id
                               && v.Code.ToLower() == codeLower, ct);
            if (codeDup)
            {
                return Result.Failure<Response>(PriceVariantErrors.CodeDuplicate);
            }

            // Overlap + spec=0 dup check (loại trừ chính nó)
            List<OverlapChecker.VariantSnapshot> siblings = await LoadSiblings(entity.PriceTableId, entity.Id, ct);
            var distinctAreaIds = request.AreaIds.Distinct().ToList();
            var draft = new OverlapChecker.VariantSnapshot(
                entity.Id,
                code,
                request.BeginTime,
                request.EndTime,
                request.DayMask,
                request.AppliesToAllAreas,
                distinctAreaIds);

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

            DateTime now = clock.UtcNow;
            int staffId = currentStaff.StaffAccountId;
            string summary = BuildSummary(entity, request, code);

            entity.Code = code;
            entity.Name = request.Name.Trim();
            entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            entity.BeginTime = request.BeginTime;
            entity.EndTime = request.EndTime;
            entity.DayMask = request.DayMask;
            entity.AppliesToAllAreas = request.AppliesToAllAreas;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = now;

            // Diff PriceVariantAreas
            List<PriceVariantArea> existing = await dbContext.PriceVariantAreas
                .Where(a => a.PriceVariantId == entity.Id)
                .ToListAsync(ct);

            if (request.AppliesToAllAreas)
            {
                if (existing.Count > 0)
                {
                    dbContext.PriceVariantAreas.RemoveRange(existing);
                }
            }
            else
            {
                var targetSet = new HashSet<int>(distinctAreaIds);
                var existingSet = existing.Select(a => a.AreaId).ToHashSet();

                var toRemove = existing.Where(a => !targetSet.Contains(a.AreaId)).ToList();
                if (toRemove.Count > 0)
                {
                    dbContext.PriceVariantAreas.RemoveRange(toRemove);
                }

                foreach (int areaId in distinctAreaIds.Where(id => !existingSet.Contains(id)))
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
                Action = "UPDATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = summary
            });

            await dbContext.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"PriceVariant.Update(id={entity.Id})", ct);

            return Result.Success(new Response(
                entity.Id, entity.PriceTableId, entity.Code, entity.Name, entity.Description,
                entity.BeginTime, entity.EndTime, entity.DayMask, entity.AppliesToAllAreas,
                request.AppliesToAllAreas ? Array.Empty<int>() : distinctAreaIds,
                OverlapChecker.Specificity(draft),
                entity.IsActive, entity.CreatedAt, entity.UpdatedAt));
        }

        private static Error? ValidateTimeRange(TimeOnly? begin, TimeOnly? end)
        {
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

        private static string BuildSummary(PriceVariant before, Command after, string normalizedCode)
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

            if (before.BeginTime != after.BeginTime || before.EndTime != after.EndTime)
            {
                diffs.Add(
                    $"time: {before.BeginTime?.ToString() ?? "*"}-{before.EndTime?.ToString() ?? "*"} → {after.BeginTime?.ToString() ?? "*"}-{after.EndTime?.ToString() ?? "*"}");
            }

            if (before.DayMask != after.DayMask)
            {
                diffs.Add($"dayMask: {before.DayMask?.ToString() ?? "all"} → {after.DayMask?.ToString() ?? "all"}");
            }

            if (before.AppliesToAllAreas != after.AppliesToAllAreas)
            {
                diffs.Add(
                    $"areaScope: {(before.AppliesToAllAreas ? "all" : "subset")} → {(after.AppliesToAllAreas ? "all" : "subset")}");
            }

            if (before.IsActive != after.IsActive)
            {
                diffs.Add($"isActive: {before.IsActive} → {after.IsActive}");
            }

            return diffs.Count == 0 ? "PriceVariant updated (no scalar changes)" : string.Join("; ", diffs);
        }
    }
}
