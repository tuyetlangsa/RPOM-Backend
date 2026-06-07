using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access.Login;

namespace Rpom.Api.Endpoints.System.Auth;

internal sealed class LoginEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/auth/login", async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new Login.Command(request.Username, request.Password),
                ct);
            return result.MatchOk();
        })
        .AllowAnonymous()
        .WithTags("Auth")
        .WithName("Login")
        .Produces<ApiResult<Login.Response>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .WithSummary("Authenticate staff by username + password; return JWT.")
        .WithDescription("Request: JSON body { username:string, password:string }. Response: 200 OK — JSON Login.Response (JWT token + profile).");
    }

    internal sealed record Request(string Username, string Password);
}
