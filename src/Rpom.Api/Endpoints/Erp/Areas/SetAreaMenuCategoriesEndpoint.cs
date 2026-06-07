using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Areas.SetAreaMenuCategories;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Areas;

internal sealed class SetAreaMenuCategoriesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/areas/{areaId:int}/menu-categories",
                async (int areaId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<SetAreaMenuCategories.Response> result = await sender.Send(
                        new SetAreaMenuCategories.Command(
                            areaId, request.CategoryIds ?? Array.Empty<int>()), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Areas")
            .WithName("SetAreaMenuCategories")
            .Produces<ApiResult<SetAreaMenuCategories.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Replace the categories assigned to an area's menu.")
            .WithDescription(
                "Request: route areaId (int); JSON body { categoryIds:int[] } (empty clears the menu). Response: 200 OK — JSON SetAreaMenuCategories.Response { areaId, inserted, deleted, total }.");
    }

    internal sealed record Request(IReadOnlyList<int>? CategoryIds);
}
