using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Access.UpdateStaffAccount;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Access;

internal sealed class UpdateStaffAccountEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/access/staff-accounts/{id:int}",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<UpdateStaffAccount.Response> result = await sender.Send(
                        new UpdateStaffAccount.Command(
                            id, request.FullName, request.Phone, request.Email,
                            request.RoleId, request.IsActive, request.IsLocked), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.StaffAccountManage)
            .WithTags("Access")
            .WithName("UpdateStaffAccount")
            .Produces<ApiResult<UpdateStaffAccount.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a staff account's info, role, and active/locked flags.");
    }

    internal sealed record Request(
        string FullName, string? Phone, string? Email, int RoleId, bool IsActive, bool IsLocked);
}
