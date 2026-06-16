using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.DeleteUomConversion;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class DeleteUomConversionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/items/{itemId:int}/uom-conversions/{id:int}",
                async (int itemId, int id, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(new DeleteUomConversion.Command(itemId, id), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Items")
            .WithName("DeleteUomConversion")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete an item UOM conversion.")
            .WithDescription("Request: route itemId (int), id (int). Response: 204 No Content.");
    }
}
