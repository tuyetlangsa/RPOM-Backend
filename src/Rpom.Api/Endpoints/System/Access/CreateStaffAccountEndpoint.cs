using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Access.CreateStaffAccount;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Access;

internal sealed class CreateStaffAccountEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/access/staff-accounts",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CreateStaffAccount.Response> result = await sender.Send(
                        new CreateStaffAccount.Command(
                            request.Username, request.Password, request.FullName,
                            request.Phone, request.Email, request.RoleId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.StaffAccountManage)
            .WithTags("Access")
            .WithName("CreateStaffAccount")
            .Produces<ApiResult<CreateStaffAccount.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a staff account (password BCrypt-hashed).");
    }

    internal sealed record Request(
        string Username, string Password, string FullName, string? Phone, string? Email, int RoleId);
}
