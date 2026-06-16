using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.GetBomLine;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class GetBomLineEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/items/{itemId:int}/bom/{id:int}",
                async (int itemId, int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetBomLine.Response> result = await sender.Send(new GetBomLine.Query(itemId, id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Items")
            .WithName("GetBomLine")
            .Produces<ApiResult<GetBomLine.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a BOM line by id.")
            .WithDescription("Request: route itemId (int), id (int). Response: 200 OK.");
    }
}
