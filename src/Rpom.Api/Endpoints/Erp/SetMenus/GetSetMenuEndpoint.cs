using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.SetMenus.GetSetMenu;

namespace Rpom.Api.Endpoints.Erp.SetMenus;

internal sealed class GetSetMenuEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/items/{itemId:int}/set-menu",
            async (int itemId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetSetMenu.Query(itemId), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("SetMenus")
            .WithName("GetSetMenu")
            .Produces<ApiResult<GetSetMenu.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get the set-menu spec of an item.")
            .WithDescription("Request: route itemId (int). Response: 200 OK — JSON GetSetMenu.Response (description + details). 404 if the item is not a set menu.");
    }
}
