using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.ListItems;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class ListItemsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/items",
                async (
                    int? categoryId,
                    string? search,
                    bool? isActive,
                    int? pageNumber,
                    int? pageSize,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    Result<Page<ListItems.Item>> result = await sender.Send(new ListItems.Query(
                        categoryId, search, isActive, pageNumber ?? 1, pageSize ?? 50), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Items")
            .WithName("ListItems")
            .Produces<ApiResult<Page<ListItems.Item>>>()
            .WithSummary("List items (paged) with optional filters.")
            .WithDescription("""
    Request: query categoryId?:int, search?:string, isActive?:bool, pageNumber?:int=1, pageSize?:int=50.
    Response: 200 OK — JSON Page<ListItems.Response> (items + pagination).
""");
    }
}
