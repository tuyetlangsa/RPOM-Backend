using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Access.ListRoles;

public static class ListRoles
{
    public sealed record Query : IQuery<Response>;

    public sealed record Response(IReadOnlyList<RoleRow> Roles);

    public sealed record RoleRow(int Id, string Code, string Name, bool IsSystemRole, int AccountCount);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var rows = await db.Roles
                .Where(r => r.IsActive)
                .OrderBy(r => r.Name)
                .Select(r => new RoleRow(
                    r.Id,
                    r.Code,
                    r.Name,
                    r.IsSystemRole,
                    db.StaffAccounts.Count(s => s.RoleId == r.Id)))
                .ToListAsync(ct);

            return Result.Success(new Response(rows));
        }
    }
}
