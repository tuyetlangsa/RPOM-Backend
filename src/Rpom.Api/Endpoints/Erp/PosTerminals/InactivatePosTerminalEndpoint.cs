using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PosTerminals.InactivatePosTerminal;

namespace Rpom.Api.Endpoints.Erp.PosTerminals;

internal sealed class InactivatePosTerminalEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/pos-terminals/{id:int}/inactivate",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new InactivatePosTerminal.Command(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .Produces<ApiResult<InactivatePosTerminal.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PosTerminals")
            .WithName("InactivatePosTerminal")
            .WithSummary("Disable POS machine (token becomes inactive).");
    }
}
