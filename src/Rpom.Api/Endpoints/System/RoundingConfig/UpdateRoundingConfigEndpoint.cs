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
            .WithName("UpdateRoundingConfig")
            .Produces<ApiResult<UpdateRoundingConfig.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update rounding precision for a config key.")
            .WithDescription("Request: route keyCode (string); JSON body { digits:short }. Response: 200 OK — JSON UpdateRoundingConfig.Response.");
    }

    internal sealed record Request(short Digits);
}
