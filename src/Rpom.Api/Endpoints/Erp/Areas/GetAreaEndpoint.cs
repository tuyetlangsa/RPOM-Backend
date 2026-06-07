using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Areas.GetArea;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Areas;

internal sealed class GetAreaEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/areas/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetArea.Response> result = await sender.Send(new GetArea.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Areas")
            .WithName("GetArea")
            .Produces<ApiResult<GetArea.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get an area by id.")
            .WithDescription("Request: route id (int). Response: 200 OK — JSON GetArea.Response.");
    }
}
