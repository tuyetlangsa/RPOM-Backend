using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PosTerminals.ListPosTerminals;

namespace Rpom.Api.Endpoints.Erp.PosTerminals;

internal sealed class ListPosTerminalsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/pos-terminals",
                async ([FromQuery] int? counterId, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new ListPosTerminals.Query(counterId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .Produces<ApiResult<ListPosTerminals.Response>>(StatusCodes.Status200OK)
            .WithTags("PosTerminals")
            .WithName("ListPosTerminals")
            .WithSummary("List of POS machines (without DeviceToken payment).");
    }
}
