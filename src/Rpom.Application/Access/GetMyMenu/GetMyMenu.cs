using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Common;

namespace Rpom.Application.Access.GetMyMenu;

public static class GetMyMenu
{
    public sealed record Query : IQuery<Response>;

    public sealed record Response(IReadOnlyList<ModuleNode> Modules);

    public sealed record ModuleNode(
        string Code,
        string Name,
        short DisplayOrder,
        IReadOnlyList<PageNode> Pages);

    public sealed record PageNode(string Code, string Name, short DisplayOrder);

    internal sealed class Handler(IDbContext db, ICurrentStaff currentStaff)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            int staffId = currentStaff.StaffAccountId;

            // Pages granted to this account, with their module — projected flat then grouped.
            var rows = await db.StaffAccountPageAccesses
                .Where(spa => spa.StaffAccountId == staffId)
                .Select(spa => new
                {
                    ModuleCode = spa.Page.Module.Code,
                    ModuleName = spa.Page.Module.Name,
                    ModuleOrder = spa.Page.Module.DisplayOrder,
                    PageCode = spa.Page.Code,
                    PageName = spa.Page.Name,
                    PageOrder = spa.Page.DisplayOrder
                })
                .ToListAsync(ct);

            var modules = rows
                .GroupBy(r => new { r.ModuleCode, r.ModuleName, r.ModuleOrder })
                .OrderBy(g => g.Key.ModuleOrder)
                .Select(g => new ModuleNode(
                    g.Key.ModuleCode,
                    g.Key.ModuleName,
                    g.Key.ModuleOrder,
                    g.OrderBy(p => p.PageOrder)
                        .Select(p => new PageNode(p.PageCode, p.PageName, p.PageOrder))
                        .ToList()))
                .ToList();

            return Result.Success(new Response(modules));
        }
    }
}
