using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Uoms.DeleteUom;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Uoms;

internal sealed class DeleteUomEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/uoms/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(new DeleteUom.Command(id), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Uoms")
            .WithName("DeleteUom")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Delete a unit of measure.")
            .WithDescription("Request: route id (int). Response: 204 No Content.");
    }
}
