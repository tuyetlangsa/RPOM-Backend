using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Configuration.UpdateConfigValue;

namespace Rpom.Api.Endpoints.System.Configurations;

internal sealed class UpdateConfigValueEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/configs/{code}",
            async (string code, [FromBody] Request request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateConfigValue.Command(code, request.Value), ct);
            return result.MatchOk();
        })
        .RequireAuthorization(Permissions.ConfigManage)
        .WithTags("Configurations")
        .WithName("UpdateConfigValue")
        .Produces<ApiResult<UpdateConfigValue.Response>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update an existing config value by code.")
        .WithDescription("Request: route code (string); JSON body { value?:string }. Response: 200 OK — JSON UpdateConfigValue.Response.");
    }

    internal sealed record Request(string? Value);
}
