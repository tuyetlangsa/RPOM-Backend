using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CancellationReasons.DeleteCancellationReason;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.CancellationReasons;

internal sealed class DeleteCancellationReasonEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/cancellation-reasons/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(new DeleteCancellationReason.Command(id), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("CancellationReasons")
            .WithName("DeleteCancellationReason")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Delete a cancellation reason.")
            .WithDescription("Request: route id (int). Response: 204 No Content.");
    }
}
