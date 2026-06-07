using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceTables.DeletePriceTable;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.PriceTables;

internal sealed class DeletePriceTableEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/price-tables/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(new DeletePriceTable.Command(id), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("PriceTables")
            .WithName("DeletePriceTable")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Delete a price table.")
            .WithDescription("Request: route id (int). Response: 204 No Content.");
    }
}
