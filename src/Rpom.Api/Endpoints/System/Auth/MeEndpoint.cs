using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access.GetMyProfile;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Auth;

internal sealed class MeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/auth/me", async (ISender sender, CancellationToken ct) =>
            {
                Result<GetMyProfile.Response> result = await sender.Send(new GetMyProfile.Query(), ct);
                return result.MatchOk();
            })
            .RequireAuthorization()
            .WithTags("Auth")
            .WithName("GetMyProfile")
            .Produces<ApiResult<GetMyProfile.Response>>()
            .WithSummary("Return the current authenticated staff profile + permissions.")
            .WithDescription(
                "Request: none (Bearer token). Response: 200 OK — JSON GetMyProfile.Response (profile + permissions).");
    }
}
