using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceEntries.ListPriceEntries;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.PriceEntries;

internal sealed class ListPriceEntriesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/price-variants/{priceVariantId:int}/entries",
                async (int priceVariantId, ISender sender, CancellationToken ct) =>
                {
                    Result<IReadOnlyList<ListPriceEntries.Response>> result =
                        await sender.Send(new ListPriceEntries.Query(priceVariantId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("PriceEntries")
            .WithName("ListPriceEntries")
            .Produces<ApiResult<IReadOnlyList<ListPriceEntries.Response>>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("List price entries of a price variant.")
            .WithDescription(
                "Request: route priceVariantId (int). Response: 200 OK — JSON array of ListPriceEntries.Response.");
    }
}
