using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceTables.ListPriceTables;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.PriceTables;

internal sealed class ListPriceTablesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/price-tables",
                async (string? search, bool? isActive, DateOnly? beginDateFrom, DateOnly? beginDateTo,
                    ISender sender, CancellationToken ct) =>
                {
                    Result<IReadOnlyList<ListPriceTables.Response>> result = await sender.Send(
                        new ListPriceTables.Query(search, isActive, beginDateFrom, beginDateTo), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("PriceTables")
            .WithName("ListPriceTables")
            .Produces<ApiResult<IReadOnlyList<ListPriceTables.Response>>>()
            .WithSummary("List price tables with optional filters.")
            .WithDescription(
                "Request: query search?:string, isActive?:bool, beginDateFrom?:date, beginDateTo?:date. Response: 200 OK — JSON array of ListPriceTables.Response.");
    }
}
