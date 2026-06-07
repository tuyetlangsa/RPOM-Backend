using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Areas.GetAreaMenuCategories;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Areas;

internal sealed class GetAreaMenuCategoriesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/areas/{areaId:int}/menu-categories",
                async (int areaId, ISender sender, CancellationToken ct) =>
                {
                    Result<GetAreaMenuCategories.Response> result =
                        await sender.Send(new GetAreaMenuCategories.Query(areaId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Areas")
            .WithName("GetAreaMenuCategories")
            .Produces<ApiResult<GetAreaMenuCategories.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("List categories assigned to an area's menu.")
            .WithDescription(
                "Request: route areaId (int). Response: 200 OK — JSON GetAreaMenuCategories.Response (directly-assigned categories).");
    }
}
