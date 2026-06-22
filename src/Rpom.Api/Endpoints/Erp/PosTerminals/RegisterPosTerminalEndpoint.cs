using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PosTerminals.RegisterPosTerminal;

namespace Rpom.Api.Endpoints.Erp.PosTerminals;

internal sealed class RegisterPosTerminalEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/pos-terminals",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new RegisterPosTerminal.Command(request.CounterId, request.Name), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .Produces<ApiResult<RegisterPosTerminal.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PosTerminals")
            .WithName("RegisterPosTerminal")
            .WithSummary("Register the POS machine for the counter → return the DeviceToken (load it into the POS machine).");
    }

    internal sealed record Request(int CounterId, string Name);
}
