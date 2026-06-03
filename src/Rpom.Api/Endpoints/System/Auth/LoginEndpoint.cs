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
        .WithSummary("Authenticate staff by username + password; return JWT.");
    }

    internal sealed record Request(string Username, string Password);
}
