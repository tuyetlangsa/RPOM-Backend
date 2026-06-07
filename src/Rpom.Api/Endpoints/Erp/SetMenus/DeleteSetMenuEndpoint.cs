using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.SetMenus.DeleteSetMenu;

namespace Rpom.Api.Endpoints.Erp.SetMenus;

internal sealed class DeleteSetMenuEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/items/{itemId:int}/set-menu",
            async (int itemId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new DeleteSetMenu.Command(itemId), ct);
                return result.MatchNoContent();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("SetMenus")
            .WithName("DeleteSetMenu")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove an item's set-menu spec (revert to SINGLE).")
            .WithDescription("Request: route itemId (int). Response: 204 No Content. 404 if the item is not a set menu.");
    }
}
