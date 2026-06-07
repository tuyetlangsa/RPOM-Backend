using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Counters.DeleteCounter;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Counters;

internal sealed class DeleteCounterEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/counters/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(new DeleteCounter.Command(id), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Counters")
            .WithName("DeleteCounter")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Delete a counter.")
            .WithDescription("Request: route id (int). Response: 204 No Content.");
    }
}
