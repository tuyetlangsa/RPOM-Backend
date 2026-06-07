using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.DiscountPolicies.CreateDiscountPolicy;

public static class CreateDiscountPolicy
{
    public sealed record ConditionInput(
        decimal? ThresholdAmount,
        int? ItemId,
        decimal? QuantityThreshold,
        int? AreaId,
        string ApplyType,
        decimal DiscountValue,
        short DisplayOrder);

    public sealed record Command(
        string Code,
        string Name,
        string? Description,
        string DiscountType,
        bool IsAutoApply,
        string? DaysOfWeek,
        bool IsActive,
        IReadOnlyList<ConditionInput> Conditions) : ICommand<Response>;

    public sealed record Response(int Id, string Code, string Name, int ConditionCount);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Description).MaximumLength(500);
            RuleFor(x => x.DaysOfWeek).MaximumLength(20);
            RuleFor(x => x.DiscountType)
                .Must(t => t == DiscountType.TicketThreshold || t == DiscountType.QuantityItem)
                .WithMessage("DiscountType must be TICKET_THRESHOLD or QUANTITY_ITEM.");
            RuleFor(x => x.Conditions).NotEmpty().WithMessage("At least one condition is required.");

            RuleForEach(x => x.Conditions).ChildRules(c =>
            {
                c.RuleFor(x => x.ApplyType)
                    .Must(t => t == DiscountApplyType.Percent || t == DiscountApplyType.Fixed)
                    .WithMessage("ApplyType must be PERCENT or FIXED.");
                c.RuleFor(x => x.DiscountValue).GreaterThanOrEqualTo(0);
                c.RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100)
                    .When(x => x.ApplyType == DiscountApplyType.Percent)
                    .WithMessage("PERCENT discount value must be 0..100.");
                c.RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo((short)0);
            });

            RuleForEach(x => x.Conditions)
                .Must((cmd, cond) => DiscountConditionRules.DiscriminatorValid(
                    cmd.DiscountType, cond.ThresholdAmount, cond.ItemId, cond.QuantityThreshold))
                .WithMessage("Condition fields don't match the policy DiscountType.");

            RuleFor(x => x.Conditions)
                .Must((cmd, conds) => !DiscountConditionRules.HasDuplicateTriggers(
                    cmd.DiscountType,
                    conds.Select(c => (c.ThresholdAmount, c.ItemId, c.QuantityThreshold, c.AreaId))))
                .WithMessage("Conditions must not duplicate the same trigger (same threshold/area or item/quantity/area).");
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
            var code = request.Code.Trim();
            var codeLower = code.ToLower();
            var duplicate = await db.DiscountPolicies.AnyAsync(p => p.Code.ToLower() == codeLower, ct);
            if (duplicate) return Result.Failure<Response>(DiscountPolicyErrors.CodeDuplicate);

            var validation = await DiscountConditionRules.ValidateReferencesAsync(db, request.Conditions
                .Select(c => (c.ItemId, c.AreaId)), ct);
            if (validation is not null) return Result.Failure<Response>(validation);

            var staffId = currentStaff.StaffAccountId;
            var now = clock.UtcNow;

            var policy = new DiscountPolicy
            {
                Code = code,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                DiscountType = request.DiscountType,
                IsAutoApply = request.IsAutoApply,
                DaysOfWeek = string.IsNullOrWhiteSpace(request.DaysOfWeek) ? null : request.DaysOfWeek.Trim(),
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now,
            };
            foreach (var c in request.Conditions)
            {
                policy.Conditions.Add(new DiscountPolicyCondition
                {
                    ThresholdAmount = c.ThresholdAmount,
                    ItemId = c.ItemId,
                    QuantityThreshold = c.QuantityThreshold,
                    AreaId = c.AreaId,
                    ApplyType = c.ApplyType,
                    DiscountValue = c.DiscountValue,
                    DisplayOrder = c.DisplayOrder,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
            db.DiscountPolicies.Add(policy);

            var staff = await db.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return Result.Failure<Response>(DiscountPolicyErrors.CodeDuplicate);
            }

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(DiscountPolicy),
                EntityId = policy.Id,
                Action = "CREATE",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"DiscountPolicy created: {policy.Code} ({request.Conditions.Count} conditions)",
            });
            await db.SaveChangesAsync(ct);
            await versionService.BumpAsync(VersionScopes.Pricing, $"DiscountPolicy.Create(id={policy.Id})", ct);

            return Result.Success(new Response(policy.Id, policy.Code, policy.Name, request.Conditions.Count));
        }
    }
}
