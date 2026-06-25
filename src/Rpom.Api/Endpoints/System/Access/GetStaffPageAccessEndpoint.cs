using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Access.GetStaffPageAccess;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Access;

internal sealed class GetStaffPageAccessEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/access/staff-accounts/{id:int}/page-access",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetStaffPageAccess.Response> result =
                        await sender.Send(new GetStaffPageAccess.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.PageAccessAssign)
            .WithTags("Access")
            .WithName("GetStaffPageAccess")
            .Produces<ApiResult<GetStaffPageAccess.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Return the full module/page catalog with granted flags for an account.")
            .WithDescription(
                "Request: route id (int). Response: 200 OK — JSON GetStaffPageAccess.Response "
                + "(every module/page, each flagged granted for the target account). 404 if account not found.");
    }
}
