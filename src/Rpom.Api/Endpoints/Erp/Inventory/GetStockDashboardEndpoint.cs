using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Inventory.GetStockDashboard;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Inventory;

internal sealed class GetStockDashboardEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/inventory/stock",
                async (
                    string? search,
                    bool? lowStock,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    Result<IReadOnlyList<GetStockDashboard.StockItem>> result =
                        await sender.Send(new GetStockDashboard.Query(search, lowStock), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Inventory")
            .WithName("GetStockDashboard")
            .Produces<ApiResult<IReadOnlyList<GetStockDashboard.StockItem>>>()
            .WithSummary("Get current stock dashboard with optional search and low-stock filter.")
            .WithDescription("""
                Request: query search?:string, lowStock?:bool.
                Response: 200 OK — JSON array of StockItem (CurrentQty, ReservedQty, thresholds, last movement).
            """);
    }
}
