using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Uoms.GetUom;

namespace Rpom.Api.Endpoints.Erp.Uoms;

internal sealed class GetUomEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/uoms/{id:int}",
            async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetUom.Query(id), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Uoms")
            .WithName("GetUom")
            .Produces<ApiResult<GetUom.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a unit of measure by id.")
            .WithDescription("Request: route id (int). Response: 200 OK — JSON GetUom.Response.");
    }
}
