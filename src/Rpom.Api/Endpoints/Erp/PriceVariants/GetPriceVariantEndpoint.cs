using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceVariants.GetPriceVariant;

namespace Rpom.Api.Endpoints.Erp.PriceVariants;

internal sealed class GetPriceVariantEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/price-variants/{id:int}",
            async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetPriceVariant.Query(id), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("PriceVariants")
            .WithName("GetPriceVariant");
    }
}
