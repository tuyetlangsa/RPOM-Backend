using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PosTerminals.UpdatePosTerminal;

namespace Rpom.Api.Endpoints.Erp.PosTerminals;

internal sealed class UpdatePosTerminalEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/pos-terminals/{id:int}",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new UpdatePosTerminal.Command(id, request.Name, request.CounterId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .Produces<ApiResult<UpdatePosTerminal.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PosTerminals")
            .WithName("UpdatePosTerminal")
            .WithSummary("Rename and move the POS machine counter (including moving the customer's connected monitor).");
    }

    internal sealed record Request(string Name, int CounterId);
}
