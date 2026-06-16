using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Shifts.GetShift;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Shifts;

internal sealed class GetShiftEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/shifts/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetShift.Response> result = await sender.Send(new GetShift.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Shifts")
            .WithName("GetShift")
            .Produces<ApiResult<GetShift.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a shift by id.")
            .WithDescription("Request: route id (int). Response: 200 OK — JSON GetShift.Response.");
    }
}
