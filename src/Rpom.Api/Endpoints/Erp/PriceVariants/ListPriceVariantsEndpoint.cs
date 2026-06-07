using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceVariants.ListPriceVariants;

namespace Rpom.Api.Endpoints.Erp.PriceVariants;

internal sealed class ListPriceVariantsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/price-tables/{priceTableId:int}/variants",
            async (int priceTableId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ListPriceVariants.Query(priceTableId), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("PriceVariants")
            .WithName("ListPriceVariants")
            .Produces<ApiResult<IReadOnlyList<ListPriceVariants.Response>>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("List price variants of a price table.")
            .WithDescription("Request: route priceTableId (int). Response: 200 OK — JSON array of ListPriceVariants.Response.");
    }
}
