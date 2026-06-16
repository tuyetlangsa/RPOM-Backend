using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Lookups.GetStockableItems;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Lookups;

internal sealed class GetStockableItemsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/lookups/stockable-items", async (
                string? search, bool? lowStock, ISender sender, CancellationToken ct) =>
            {
                Result<IReadOnlyList<GetStockableItems.StockableItem>> result =
                    await sender.Send(new GetStockableItems.Query(search, lowStock), ct);
                return result.MatchOk();
            })
            .RequireAuthorization()
            .WithTags("Lookups")
            .WithName("GetStockableItems")
            .Produces<ApiResult<IReadOnlyList<GetStockableItems.StockableItem>>>()
            .WithSummary("List all active stockable items with current stock (for stock-in, BOM material picker, dashboard).")
            .WithDescription(
                "Request: optional ?search= (code/name) & ?lowStock=true (only items at/below LowStockThreshold). "
                + "Response: 200 OK — items with IsStockable=true; never-stocked items show currentQty=0, lastMovementAt=null.");
    }
}
