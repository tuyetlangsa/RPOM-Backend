using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceTables.ListPriceTables;

namespace Rpom.Api.Endpoints.Erp.PriceTables;

internal sealed class ListPriceTablesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/price-tables",
            async (string? search, bool? isActive, DateOnly? beginDateFrom, DateOnly? beginDateTo,
                   ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(
                    new ListPriceTables.Query(search, isActive, beginDateFrom, beginDateTo), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("PriceTables")
            .WithName("ListPriceTables");
    }
}
