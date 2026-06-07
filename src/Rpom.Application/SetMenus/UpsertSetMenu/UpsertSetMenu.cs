using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.SetMenus.UpsertSetMenu;

/// <summary>
/// Create-or-update the SET_MENU aspect of an existing Item: upsert the SetMenu
/// row (its existence marks the Item as SET_MENU) and replace-all its detail rows
/// (COMPONENT dishes + CHOICE_CATEGORY groups). Bump scope MENU.
/// </summary>
public static class UpsertSetMenu
{
    public sealed record DetailInput(
        string DetailType,
        int? ComponentItemId,
        decimal? Quantity,
        bool? IsFixed,
        int? ChoiceCategoryId,
        short DisplayOrder);

    public sealed record Command(
        int ItemId,
        string? Description,
        IReadOnlyList<DetailInput> Details) : ICommand<Response>;

    public sealed record Response(int ItemId, int DetailCount);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ItemId).GreaterThan(0);
            RuleFor(x => x.Details).NotNull();
            RuleForEach(x => x.Details).ChildRules(d =>
            {
                d.RuleFor(x => x.DetailType)
                    .Must(t => t == SetMenuDetailType.Component || t == SetMenuDetailType.ChoiceCategory)
                    .WithMessage("DetailType must be COMPONENT or CHOICE_CATEGORY.");

                // COMPONENT: needs ComponentItemId + Quantity(>0) + IsFixed; no ChoiceCategoryId.
                d.RuleFor(x => x.ComponentItemId).NotNull()
                    .When(x => x.DetailType == SetMenuDetailType.Component);
                d.RuleFor(x => x.Quantity).NotNull().GreaterThan(0)
                    .When(x => x.DetailType == SetMenuDetailType.Component);
                d.RuleFor(x => x.IsFixed).NotNull()
                    .When(x => x.DetailType == SetMenuDetailType.Component);
                d.RuleFor(x => x.ChoiceCategoryId).Null()
                    .When(x => x.DetailType == SetMenuDetailType.Component);

                // CHOICE_CATEGORY: needs ChoiceCategoryId; no component fields.
                d.RuleFor(x => x.ChoiceCategoryId).NotNull()
                    .When(x => x.DetailType == SetMenuDetailType.ChoiceCategory);
                d.RuleFor(x => x.ComponentItemId).Null()
                    .When(x => x.DetailType == SetMenuDetailType.ChoiceCategory);
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
            var itemExists = await db.Items.AnyAsync(i => i.Id == request.ItemId, ct);
            if (!itemExists) return Result.Failure<Response>(ItemErrors.NotFound);

            var componentIds = request.Details
                .Where(d => d.DetailType == SetMenuDetailType.Component && d.ComponentItemId.HasValue)
                .Select(d => d.ComponentItemId!.Value).Distinct().ToList();

            if (componentIds.Contains(request.ItemId))
                return Result.Failure<Response>(SetMenuErrors.SelfComponent);
            if (componentIds.Count > 0)
            {
                var found = await db.Items.CountAsync(i => componentIds.Contains(i.Id), ct);
                if (found != componentIds.Count)
                    return Result.Failure<Response>(SetMenuErrors.ComponentNotFound);
            }

            var choiceCategoryIds = request.Details
                .Where(d => d.DetailType == SetMenuDetailType.ChoiceCategory && d.ChoiceCategoryId.HasValue)
                .Select(d => d.ChoiceCategoryId!.Value).Distinct().ToList();
            if (choiceCategoryIds.Count > 0)
            {
                var found = await db.ChoiceCategories
                    .CountAsync(c => choiceCategoryIds.Contains(c.Id) && c.IsActive, ct);
                if (found != choiceCategoryIds.Count)
                    return Result.Failure<Response>(SetMenuErrors.ChoiceCategoryNotFound);
            }

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;

            var setMenu = await db.SetMenus
                .FirstOrDefaultAsync(s => s.ItemId == request.ItemId, ct);
            if (setMenu is null)
            {
                setMenu = new SetMenu
                {
                    ItemId = request.ItemId,
                    Description = request.Description,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.SetMenus.Add(setMenu);
            }
            else
            {
                setMenu.Description = request.Description;
                setMenu.UpdatedAt = now;
            }

            // Replace-all details.
            var existing = await db.SetMenuDetails
                .Where(d => d.SetMenuItemId == request.ItemId)
                .ToListAsync(ct);
            db.SetMenuDetails.RemoveRange(existing);

            foreach (var d in request.Details)
            {
                db.SetMenuDetails.Add(new SetMenuDetail
                {
                    SetMenuItemId = request.ItemId,
                    DetailType = d.DetailType,
                    ComponentItemId = d.DetailType == SetMenuDetailType.Component ? d.ComponentItemId : null,
                    Quantity = d.DetailType == SetMenuDetailType.Component ? d.Quantity : null,
                    IsFixed = d.DetailType == SetMenuDetailType.Component ? d.IsFixed : null,
                    ChoiceCategoryId = d.DetailType == SetMenuDetailType.ChoiceCategory ? d.ChoiceCategoryId : null,
                    DisplayOrder = d.DisplayOrder,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            var staff = await db.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(SetMenu),
                EntityId = request.ItemId,
                Action = "UPSERT",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"SetMenu upsert: {request.Details.Count} details on item {request.ItemId}",
            });

            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Menu, $"SetMenu.Upsert(itemId={request.ItemId})", ct);

            return Result.Success(new Response(request.ItemId, request.Details.Count));
        }
    }
}
