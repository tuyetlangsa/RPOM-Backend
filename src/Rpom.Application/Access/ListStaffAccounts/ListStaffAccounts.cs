using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Access.ListStaffAccounts;

public static class ListStaffAccounts
{
    public sealed record Query(
        int? RoleId,
        string? Search,
        int PageNumber,
        int PageSize) : IQuery<Page<Row>>;

    public sealed record Row(
        int Id,
        string Username,
        string FullName,
        string? Phone,
        string RoleCode,
        string RoleName,
        bool IsActive,
        bool IsLocked);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
        }
    }

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Page<Row>>
    {
        public async Task<Result<Page<Row>>> Handle(Query request, CancellationToken ct)
        {
            IQueryable<Domain.Access.StaffAccount> q = db.StaffAccounts.AsQueryable();

            if (request.RoleId.HasValue)
            {
                q = q.Where(x => x.RoleId == request.RoleId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                string s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Username.ToLower().Contains(s) || x.FullName.ToLower().Contains(s));
            }

            int total = await q.CountAsync(ct);

            List<Row> rows = await q
                .OrderBy(x => x.Username)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => new Row(
                    x.Id, x.Username, x.FullName, x.Phone,
                    x.Role.Code, x.Role.Name, x.IsActive, x.IsLocked))
                .ToListAsync(ct);

            return Result.Success(new Page<Row>(rows, total, request.PageNumber, request.PageSize));
        }
    }
}
