using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Access.GetRolePermissionDefault;

public static class GetRolePermissionDefault
{
    public sealed record Query(string RoleCode) : IQuery<Response>;

    public sealed record Response(string RoleCode, IReadOnlyList<string> PermissionCodes);

    internal sealed class Handler : IQueryHandler<Query, Response>
    {
        public Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            IReadOnlyList<string> perms = RolePermissionDefaults.ForRole(request.RoleCode);
            return Task.FromResult(Result.Success(new Response(request.RoleCode, perms)));
        }
    }
}
