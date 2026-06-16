using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.KitchenStations.DeleteKitchenStation;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.KitchenStations;

internal sealed class DeleteKitchenStationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/kitchen-stations/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(new DeleteKitchenStation.Command(id), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("KitchenStations")
            .WithName("DeleteKitchenStation")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Delete a kitchen station.")
            .WithDescription("Request: route id (int). Response: 204 No Content.");
    }
}
