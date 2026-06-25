using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Access.SetStaffPageAccess;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Access;

internal sealed class SetStaffPageAccessEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/access/staff-accounts/{id:int}/page-access",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<SetStaffPageAccess.Response> result =
                        await sender.Send(
                            new SetStaffPageAccess.Command(id, request.PageCodes ?? []), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.PageAccessAssign)
            .WithTags("Access")
            .WithName("SetStaffPageAccess")
            .Produces<ApiResult<SetStaffPageAccess.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Full-replace the page-access grants for an account.")
            .WithDescription(
                "Request: route id (int); JSON body { pageCodes: string[] }. Replaces the account's "
                + "entire page-access set. Response: 200 OK — JSON SetStaffPageAccess.Response. "
                + "400 if any page code is unknown; 404 if account not found.");
    }

    internal sealed record Request(IReadOnlyList<string>? PageCodes);
}
