using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.DeleteItem;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class DeleteItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/items/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(new DeleteItem.Command(id), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Items")
            .WithName("DeleteItem")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Delete an item.")
            .WithDescription("Request: route id (int). Response: 204 No Content.");
    }
}
