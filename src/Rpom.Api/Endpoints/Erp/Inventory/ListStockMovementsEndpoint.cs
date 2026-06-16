using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Inventory.ListStockMovements;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Inventory;

internal sealed class ListStockMovementsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/stock-movements",
                async (
                    int? itemId,
                    string? movementType,
                    DateTime? fromDate,
                    DateTime? toDate,
                    int? pageIndex,
                    int? pageSize,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    Result<Page<ListStockMovements.Response>> result =
                        await sender.Send(new ListStockMovements.Query(
                            itemId, movementType, fromDate, toDate, pageIndex ?? 1, pageSize ?? 50), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Inventory")
            .WithName("ListStockMovements")
            .Produces<ApiResult<Page<ListStockMovements.Response>>>()
            .WithSummary("List stock movements (paginated) with optional filters.")
            .WithDescription("""
                Request: query itemId?:int, movementType?:string, fromDate?:DateTime, toDate?:DateTime, pageIndex?:int=1, pageSize?:int=50.
                Response: 200 OK — JSON Page of stock movement lines.
            """);
    }
}
