using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Access.ResetPassword;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Access;

internal sealed class ResetPasswordEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/access/staff-accounts/{id:int}/password",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(new ResetPassword.Command(id, request.NewPassword), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.StaffAccountManage)
            .WithTags("Access")
            .WithName("ResetPassword")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Reset a staff account's password (BCrypt).");
    }

    internal sealed record Request(string NewPassword);
}
