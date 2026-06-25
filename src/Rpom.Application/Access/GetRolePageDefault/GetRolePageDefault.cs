using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Access.GetRolePageDefault;

public static class GetRolePageDefault
{
    public sealed record Query(string RoleCode) : IQuery<Response>;

    public sealed record Response(string RoleCode, IReadOnlyList<string> PageCodes);

    internal sealed class Handler : IQueryHandler<Query, Response>
    {
        public Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            IReadOnlyList<string> pages = RolePageDefaults.ForRole(request.RoleCode);
            return Task.FromResult(Result.Success(new Response(request.RoleCode, pages)));
        }
    }
}
