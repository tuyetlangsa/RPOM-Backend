using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Configuration.GetConfigValues;

namespace Rpom.Api.Endpoints.System.Configurations;

internal sealed class GetConfigValuesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/configs", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetConfigValues.Query(), ct);
            return result.MatchOk();
        })
        .RequireAuthorization(Permissions.ConfigView)
        .WithTags("Configurations")
        .WithName("GetConfigValues")
        .WithSummary("List all config values (Owner/Manager).");
    }
}
