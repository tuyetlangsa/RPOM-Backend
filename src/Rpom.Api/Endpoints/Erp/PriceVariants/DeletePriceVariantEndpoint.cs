using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceVariants.DeletePriceVariant;

namespace Rpom.Api.Endpoints.Erp.PriceVariants;

internal sealed class DeletePriceVariantEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/price-variants/{id:int}",
            async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new DeletePriceVariant.Command(id), ct);
                return result.MatchNoContent();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("PriceVariants")
            .WithName("DeletePriceVariant");
    }
}
