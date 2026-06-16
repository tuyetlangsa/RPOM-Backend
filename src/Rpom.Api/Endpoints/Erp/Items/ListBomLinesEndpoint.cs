using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.ListBomLines;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class ListBomLinesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/items/{itemId:int}/bom",
                async (int itemId, bool? isActive, ISender sender, CancellationToken ct) =>
                {
                    Result<IReadOnlyList<ListBomLines.Response>> result =
                        await sender.Send(new ListBomLines.Query(itemId, isActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Items")
            .WithName("ListBomLines")
            .Produces<ApiResult<IReadOnlyList<ListBomLines.Response>>>()
            .WithSummary("List BOM lines for an item.")
            .WithDescription("Request: route itemId (int); query isActive?:bool. Response: 200 OK.");
    }
}
