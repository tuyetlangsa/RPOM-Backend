using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.GetUomConversion;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class GetUomConversionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/items/{itemId:int}/uom-conversions/{id:int}",
                async (int itemId, int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetUomConversion.Response> result = await sender.Send(new GetUomConversion.Query(itemId, id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Items")
            .WithName("GetUomConversion")
            .Produces<ApiResult<GetUomConversion.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get an item UOM conversion by id.")
            .WithDescription("Request: route itemId (int), id (int). Response: 200 OK.");
    }
}
