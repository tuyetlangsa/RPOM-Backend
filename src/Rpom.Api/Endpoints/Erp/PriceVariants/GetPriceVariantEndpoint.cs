using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceVariants.GetPriceVariant;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.PriceVariants;

internal sealed class GetPriceVariantEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/price-variants/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetPriceVariant.Response> result = await sender.Send(new GetPriceVariant.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("PriceVariants")
            .WithName("GetPriceVariant")
            .Produces<ApiResult<GetPriceVariant.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a price variant by id.")
            .WithDescription("Request: route id (int). Response: 200 OK — JSON GetPriceVariant.Response.");
    }
}
