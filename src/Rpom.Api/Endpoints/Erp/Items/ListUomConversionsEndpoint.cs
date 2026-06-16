using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.ListUomConversions;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class ListUomConversionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/items/{itemId:int}/uom-conversions",
                async (int itemId, bool? isActive, ISender sender, CancellationToken ct) =>
                {
                    Result<IReadOnlyList<ListUomConversions.Response>> result =
                        await sender.Send(new ListUomConversions.Query(itemId, isActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Items")
            .WithName("ListUomConversions")
            .Produces<ApiResult<IReadOnlyList<ListUomConversions.Response>>>()
            .WithSummary("List UOM conversions for an item.")
            .WithDescription("Request: route itemId (int); query isActive?:bool. Response: 200 OK.");
    }
}
