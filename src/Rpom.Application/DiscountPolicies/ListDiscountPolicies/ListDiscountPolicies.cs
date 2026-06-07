using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.DiscountPolicies.ListDiscountPolicies;

public static class ListDiscountPolicies
{
    public sealed record Query(string? Search, bool? IsActive, string? DiscountType)
        : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
        string DiscountType,
        bool IsAutoApply,
        string? DaysOfWeek,
        bool IsActive,
        int ConditionCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            IQueryable<DiscountPolicy> q = db.DiscountPolicies.AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                string s = request.Search.Trim().ToLower();
                q = q.Where(p => p.Code.ToLower().Contains(s) || p.Name.ToLower().Contains(s));
            }

            if (request.IsActive is { } active)
            {
                q = q.Where(p => p.IsActive == active);
            }

            if (!string.IsNullOrWhiteSpace(request.DiscountType))
            {
                q = q.Where(p => p.DiscountType == request.DiscountType);
            }

            List<Response> list = await q
                .OrderBy(p => p.Code)
                .Select(p => new Response(
                    p.Id, p.Code, p.Name, p.Description, p.DiscountType, p.IsAutoApply,
                    p.DaysOfWeek, p.IsActive, p.Conditions.Count, p.CreatedAt, p.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(list);
        }
    }
}
