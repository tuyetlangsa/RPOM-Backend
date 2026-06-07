using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.GetItem;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class GetItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/items/{id:int}",
            async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetItem.Query(id), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Items")
            .WithName("GetItem")
            .Produces<ApiResult<GetItem.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get an item by id.")
            .WithDescription("Request: route id (int). Response: 200 OK — JSON GetItem.Response.");
    }
}
