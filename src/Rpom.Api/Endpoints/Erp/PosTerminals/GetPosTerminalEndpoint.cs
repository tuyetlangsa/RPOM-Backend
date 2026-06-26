using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PosTerminals.GetPosTerminal;

namespace Rpom.Api.Endpoints.Erp.PosTerminals;

internal sealed class GetPosTerminalEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/pos-terminals/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new GetPosTerminal.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .Produces<ApiResult<GetPosTerminal.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PosTerminals")
            .WithName("GetPosTerminal")
            .WithSummary("Details of 1 POS machine by ID (DeviceToken not provided).");
    }
}
