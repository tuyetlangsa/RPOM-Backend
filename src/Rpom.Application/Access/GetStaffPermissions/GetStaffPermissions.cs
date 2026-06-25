using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Access;
using Rpom.Domain.Common;

namespace Rpom.Application.Access.GetStaffPermissions;

public static class GetStaffPermissions
{
    public sealed record Query(int StaffAccountId) : IQuery<Response>;

    public sealed record Response(IReadOnlyList<Group> Groups);

    public sealed record Group(string Code, string Name, IReadOnlyList<PermissionRow> Permissions);

    public sealed record PermissionRow(string Code, string Name, bool Granted);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            bool exists = await db.StaffAccounts.AnyAsync(x => x.Id == request.StaffAccountId, ct);
            if (!exists)
            {
                return Result.Failure<Response>(AccessErrors.StaffNotFound);
            }

            var grantedIds = (await db.StaffAccountPermissions
                .Where(x => x.StaffAccountId == request.StaffAccountId)
                .Select(x => x.PermissionId)
                .ToListAsync(ct)).ToHashSet();

            var groups = await db.PermissionGroups
                .OrderBy(g => g.DisplayOrder)
                .Select(g => new
                {
                    g.Code,
                    g.Name,
                    Permissions = g.Permissions
                        .OrderBy(p => p.DisplayOrder)
                        .Select(p => new { p.Id, p.Code, p.Name })
                        .ToList()
                })
                .ToListAsync(ct);

            var response = new Response(groups
                .Select(g => new Group(
                    g.Code,
                    g.Name,
                    g.Permissions
                        .Select(p => new PermissionRow(p.Code, p.Name, grantedIds.Contains(p.Id)))
                        .ToList()))
                .ToList());

            return Result.Success(response);
        }
    }
}
