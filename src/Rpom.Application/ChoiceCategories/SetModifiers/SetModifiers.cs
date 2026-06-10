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

namespace Rpom.Application.ChoiceCategories.SetModifiers;

/// <summary>
///     Full-snapshot replace of a ChoiceCategory's modifiers. FE gửi nguyên list
///     modifier hiện tại; BE diff theo khoá kép (ChoiceCategoryId, ItemId): thêm,
///     cập nhật, xoá row không có trong payload. Mirror UpsertPriceEntries.
/// </summary>
public static class SetModifiers
{
    public sealed record ModifierInput(
        int ItemId,
        decimal ExtraPrice,
        int MinPerModifier,
        int MaxPerModifier,
        short DisplayOrder,
        bool IsActive);

    public sealed record Command(
        int ChoiceCategoryId,
        IReadOnlyList<ModifierInput> Modifiers) : ICommand<Response>;

    public sealed record Response(
        int ChoiceCategoryId,
        int Inserted,
        int Updated,
        int Deleted,
        int Total);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ChoiceCategoryId).GreaterThan(0);
            RuleFor(x => x.Modifiers).NotNull();
            RuleForEach(x => x.Modifiers).ChildRules(m =>
            {
                m.RuleFor(x => x.ItemId).GreaterThan(0);
                m.RuleFor(x => x.ExtraPrice).GreaterThanOrEqualTo(0);
                m.RuleFor(x => x.MinPerModifier).GreaterThanOrEqualTo(0);
                m.RuleFor(x => x.MaxPerModifier).GreaterThanOrEqualTo(1);
            });
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
            var cc = await db.ChoiceCategories
                .Where(c => c.Id == request.ChoiceCategoryId)
                .Select(c => new { c.MinChoice, c.MaxChoice })
                .FirstOrDefaultAsync(ct);
            if (cc is null)
            {
                return Result.Failure<Response>(ChoiceCategoryErrors.NotFound);
            }

            // A modifier's MaxPerModifier must not exceed the category's MaxChoice.
            // Otherwise the modifier could never be fully selected (blocked by MaxChoice).
            if (cc.MaxChoice != null)
            {
                var overshoot = request.Modifiers
                    .Where(m => m.MaxPerModifier > cc.MaxChoice.Value)
                    .Select(m => m.ItemId).ToList();
                if (overshoot.Count > 0)
                {
                    return Result.Failure<Response>(ChoiceCategoryErrors.MaxPerModifierExceedsMaxChoice(
                        overshoot.First(), cc.MaxChoice.Value));
                }
            }

            // MinPerModifier must not exceed MaxPerModifier on the same modifier row.
            var inverted = request.Modifiers
                .Where(m => m.MinPerModifier > m.MaxPerModifier)
                .Select(m => m.ItemId).ToList();
            if (inverted.Count > 0)
            {
                return Result.Failure<Response>(ChoiceCategoryErrors.MinPerModifierExceedsMaxPerModifier(
                    inverted.First()));
            }

            // Sum of MinPerModifier across all modifiers must not exceed MaxChoice.
            // Otherwise it's impossible to satisfy every modifier's minimum.
            if (cc.MaxChoice != null && request.Modifiers.Count > 0)
            {
                int sumMin = request.Modifiers.Sum(m => m.MinPerModifier);
                if (sumMin > cc.MaxChoice.Value)
                {
                    return Result.Failure<Response>(ChoiceCategoryErrors.MinPerModifierSumExceedsMaxChoice(
                        sumMin, cc.MaxChoice.Value));
                }
            }

            var itemIds = request.Modifiers.Select(m => m.ItemId).ToList();
            if (itemIds.Count != itemIds.Distinct().Count())
            {
                return Result.Failure<Response>(ChoiceCategoryErrors.DuplicateModifierItem);
            }

            if (itemIds.Count > 0)
            {
                var distinct = itemIds.Distinct().ToList();
                int found = await db.Items.CountAsync(i => distinct.Contains(i.Id), ct);
                if (found != distinct.Count)
                {
                    return Result.Failure<Response>(ChoiceCategoryErrors.ItemNotFound);
                }
            }

            List<Modifier> existing = await db.Modifiers
                .Where(m => m.ChoiceCategoryId == request.ChoiceCategoryId)
                .ToListAsync(ct);
            var existingByItem = existing.ToDictionary(m => m.ItemId);
            var payloadByItem = request.Modifiers.ToDictionary(m => m.ItemId);

            DateTime now = clock.UtcNow;
            int inserted = 0;
            int updated = 0;
            int deleted = 0;

            foreach (ModifierInput input in request.Modifiers)
            {
                if (existingByItem.TryGetValue(input.ItemId, out Modifier? row))
                {
                    if (row.ExtraPrice != input.ExtraPrice
                        || row.MinPerModifier != input.MinPerModifier
                        || row.MaxPerModifier != input.MaxPerModifier
                        || row.DisplayOrder != input.DisplayOrder
                        || row.IsActive != input.IsActive)
                    {
                        row.ExtraPrice = input.ExtraPrice;
                        row.MinPerModifier = input.MinPerModifier;
                        row.MaxPerModifier = input.MaxPerModifier;
                        row.DisplayOrder = input.DisplayOrder;
                        row.IsActive = input.IsActive;
                        row.UpdatedAt = now;
                        updated++;
                    }
                }
                else
                {
                    db.Modifiers.Add(new Modifier
                    {
                        ChoiceCategoryId = request.ChoiceCategoryId,
                        ItemId = input.ItemId,
                        ExtraPrice = input.ExtraPrice,
                        MinPerModifier = input.MinPerModifier,
                        MaxPerModifier = input.MaxPerModifier,
                        DisplayOrder = input.DisplayOrder,
                        IsActive = input.IsActive,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                    inserted++;
                }
            }

            foreach (Modifier row in existing)
            {
                if (!payloadByItem.ContainsKey(row.ItemId))
                {
                    db.Modifiers.Remove(row);
                    deleted++;
                }
            }

            if (inserted > 0 || updated > 0 || deleted > 0)
            {
                int staffId = currentStaff.StaffAccountId;
                StaffAccount staff = await db.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
                db.AuditLogs.Add(new AuditLog
                {
                    EntityType = nameof(ChoiceCategory),
                    EntityId = request.ChoiceCategoryId,
                    Action = "SET_MODIFIERS",
                    ActorStaffAccountId = staffId,
                    ActorFullName = staff.FullName,
                    Timestamp = now,
                    Summary = $"Modifiers set: +{inserted} ~{updated} -{deleted} on CC {request.ChoiceCategoryId}"
                });
                await db.SaveChangesAsync(ct);
                await versionService.BumpAsync(VersionScopes.Menu,
                    $"ChoiceCategory.SetModifiers(id={request.ChoiceCategoryId})", ct);
            }

            return Result.Success(new Response(
                request.ChoiceCategoryId, inserted, updated, deleted, request.Modifiers.Count));
        }
    }
}
