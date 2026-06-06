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
            .WithName("ListPriceVariants");
    }
}
