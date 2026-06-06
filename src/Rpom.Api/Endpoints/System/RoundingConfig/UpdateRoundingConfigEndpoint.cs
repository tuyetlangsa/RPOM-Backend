using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Configuration.UpdateRoundingConfig;

namespace Rpom.Api.Endpoints.System.RoundingConfig;

internal sealed class UpdateRoundingConfigEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/rounding-configs/{keyCode}",
            async (string keyCode, [FromBody] Request body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(
                    new UpdateRoundingConfig.Command(keyCode, body.Digits), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.UpdateRoundingConfig)
            .WithTags("RoundingConfig")
            .WithName("UpdateRoundingConfig");
    }

    internal sealed record Request(short Digits);
}
