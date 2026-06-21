using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Access.ListStaffAccounts;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Access;

internal sealed class ListStaffAccountsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/access/staff-accounts",
                async (int? roleId, string? search, int? pageNumber, int? pageSize,
                    ISender sender, CancellationToken ct) =>
                {
                    Result<Page<ListStaffAccounts.Row>> result = await sender.Send(
                        new ListStaffAccounts.Query(roleId, search, pageNumber ?? 1, pageSize ?? 50), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.StaffAccountManage)
            .WithTags("Access")
            .WithName("ListStaffAccounts")
            .Produces<ApiResult<Page<ListStaffAccounts.Row>>>()
            .WithSummary("List staff accounts (paged) filtered by role + search.");
    }
}
