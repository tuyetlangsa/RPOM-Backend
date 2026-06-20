using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Access;
using Rpom.Domain.Common;

namespace Rpom.Application.Access.GetStaffPageAccess;

public static class GetStaffPageAccess
{
    public sealed record Query(int StaffAccountId) : IQuery<Response>;

    public sealed record Response(IReadOnlyList<ModuleGroup> Modules);

    public sealed record ModuleGroup(string Code, string Name, IReadOnlyList<PageRow> Pages);

    public sealed record PageRow(string Code, string Name, bool Granted);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            bool exists = await db.StaffAccounts
                .AnyAsync(x => x.Id == request.StaffAccountId, ct);
            if (!exists)
            {
                return Result.Failure<Response>(AccessErrors.StaffNotFound);
            }

            var grantedPageIds = (await db.StaffAccountPageAccesses
                .Where(x => x.StaffAccountId == request.StaffAccountId)
                .Select(x => x.PageId)
                .ToListAsync(ct)).ToHashSet();

            var modules = await db.Modules
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new
                {
                    m.Code,
                    m.Name,
                    Pages = m.Pages
                        .OrderBy(p => p.DisplayOrder)
                        .Select(p => new { p.Id, p.Code, p.Name })
                        .ToList()
                })
                .ToListAsync(ct);

            var response = new Response(modules
                .Select(m => new ModuleGroup(
                    m.Code,
                    m.Name,
                    m.Pages
                        .Select(p => new PageRow(p.Code, p.Name, grantedPageIds.Contains(p.Id)))
                        .ToList()))
                .ToList());

            return Result.Success(response);
        }
    }
}
