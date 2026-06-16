using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.DeleteBomLine;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class DeleteBomLineEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/items/{itemId:int}/bom/{id:int}",
                async (int itemId, int id, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(new DeleteBomLine.Command(itemId, id), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Items")
            .WithName("DeleteBomLine")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete a BOM line.")
            .WithDescription("Request: route itemId (int), id (int). Response: 204 No Content.");
    }
}
