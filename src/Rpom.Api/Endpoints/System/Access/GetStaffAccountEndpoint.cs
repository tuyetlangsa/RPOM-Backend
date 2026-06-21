using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Access.GetStaffAccount;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Access;

internal sealed class GetStaffAccountEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/access/staff-accounts/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetStaffAccount.Response> result =
                        await sender.Send(new GetStaffAccount.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.StaffAccountManage)
            .WithTags("Access")
            .WithName("GetStaffAccount")
            .Produces<ApiResult<GetStaffAccount.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get one staff account detail.");
    }
}
