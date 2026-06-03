using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access.GetMyProfile;

namespace Rpom.Api.Endpoints.System.Auth;

internal sealed class MeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/auth/me", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyProfile.Query(), ct);
            return result.MatchOk();
        })
        .RequireAuthorization()
        .WithTags("Auth")
        .WithName("GetMyProfile")
        .WithSummary("Return the current authenticated staff profile + permissions.");
    }
}
