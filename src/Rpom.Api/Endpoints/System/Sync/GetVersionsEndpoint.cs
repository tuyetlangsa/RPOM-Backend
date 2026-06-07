using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Sync.GetVersions;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Sync;

internal sealed class GetVersionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/sync/versions",
                async (string? scopes, ISender sender, CancellationToken ct) =>
                {
                    string[] list = string.IsNullOrWhiteSpace(scopes)
                        ? Array.Empty<string>()
                        : scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    Result<GetVersions.Response> result = await sender.Send(new GetVersions.Query(list), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization() // any authed user — no specific permission
            .WithTags("Sync")
            .WithName("GetSyncVersions")
            .Produces<ApiResult<GetVersions.Response>>()
            .WithSummary("Get current versions of given scopes for cross-aggregate polling.")
            .WithDescription(
                "Request: query scopes (comma-separated scope names). Response: 200 OK — JSON GetVersions.Response (scope to version map).");
    }
}
