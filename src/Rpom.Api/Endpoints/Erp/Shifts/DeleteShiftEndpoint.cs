using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Shifts.DeleteShift;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Shifts;

internal sealed class DeleteShiftEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/shifts/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(new DeleteShift.Command(id), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Shifts")
            .WithName("DeleteShift")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Delete a shift.")
            .WithDescription("Request: route id (int). Response: 204 No Content.");
    }
}
