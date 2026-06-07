using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceTables.GetPriceTable;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.PriceTables;

internal sealed class GetPriceTableEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/price-tables/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetPriceTable.Response> result = await sender.Send(new GetPriceTable.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("PriceTables")
            .WithName("GetPriceTable")
            .Produces<ApiResult<GetPriceTable.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a price table by id.")
            .WithDescription("Request: route id (int). Response: 200 OK — JSON GetPriceTable.Response.");
    }
}
