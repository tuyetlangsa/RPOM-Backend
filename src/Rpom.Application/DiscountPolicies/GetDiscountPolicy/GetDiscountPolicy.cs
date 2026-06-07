using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.DiscountPolicies.GetDiscountPolicy;

public static class GetDiscountPolicy
{
    public sealed record Query(int Id) : IQuery<Response>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
        string DiscountType,
        bool IsAutoApply,
        string? DaysOfWeek,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        IReadOnlyList<ConditionRef> Conditions);

    public sealed record ConditionRef(
        int Id,
        decimal? ThresholdAmount,
        int? ItemId,
        string? ItemName,
        decimal? QuantityThreshold,
        int? AreaId,
        string? AreaName,
        string ApplyType,
        decimal DiscountValue,
        short DisplayOrder);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var policy = await db.DiscountPolicies
                .Where(p => p.Id == request.Id)
                .Select(p => new
                {
                    p.Id, p.Code, p.Name, p.Description, p.DiscountType, p.IsAutoApply,
                    p.DaysOfWeek, p.IsActive, p.CreatedAt, p.UpdatedAt
                })
                .FirstOrDefaultAsync(ct);
            if (policy is null)
            {
                return Result.Failure<Response>(DiscountPolicyErrors.NotFound);
            }

            List<ConditionRef> conditions = await db.DiscountPolicyConditions
                .Where(c => c.DiscountPolicyId == request.Id)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new ConditionRef(
                    c.Id, c.ThresholdAmount, c.ItemId,
                    c.Item != null ? c.Item.Name : null,
                    c.QuantityThreshold, c.AreaId,
                    c.Area != null ? c.Area.Name : null,
                    c.ApplyType, c.DiscountValue, c.DisplayOrder))
                .ToListAsync(ct);

            return Result.Success(new Response(
                policy.Id, policy.Code, policy.Name, policy.Description, policy.DiscountType,
                policy.IsAutoApply, policy.DaysOfWeek, policy.IsActive,
                policy.CreatedAt, policy.UpdatedAt, conditions));
        }
    }
}
