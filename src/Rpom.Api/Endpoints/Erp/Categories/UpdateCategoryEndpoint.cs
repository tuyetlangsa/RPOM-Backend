using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Categories.UpdateCategory;

namespace Rpom.Api.Endpoints.Erp.Categories;

internal sealed class UpdateCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/categories/{id:int}",
            async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new UpdateCategory.Command(
                    id, request.Code, request.Name, request.Description, request.ParentId,
                    request.DisplayOrder, request.IsActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Categories")
            .WithName("UpdateCategory")
            .Produces<ApiResult<UpdateCategory.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update a category.")
            .WithDescription("Request: route id (int); JSON body { code:string, name:string, description?:string, parentId?:int, displayOrder:short, isActive:bool }. Response: 200 OK — JSON UpdateCategory.Response.");
    }

    internal sealed record Request(
        string Code, string Name, string? Description, int? ParentId,
        short DisplayOrder, bool IsActive);
}
