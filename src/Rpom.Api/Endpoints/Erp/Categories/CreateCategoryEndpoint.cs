using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Categories.CreateCategory;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Categories;

internal sealed class CreateCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/categories",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CreateCategory.Response> result = await sender.Send(new CreateCategory.Command(
                        request.Code, request.Name, request.Description, request.ParentId,
                        request.DisplayOrder, request.IsActive), ct);
                    return result.MatchCreated(c => $"/api/categories/{c.Id}");
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Categories")
            .WithName("CreateCategory")
            .Produces<ApiResult<CreateCategory.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a category.")
            .WithDescription(
                "Request: JSON body { code:string, name:string, description?:string, parentId?:int, displayOrder:short, isActive:bool }. Response: 201 Created — Location header; JSON body with new category id.");
    }

    internal sealed record Request(
        string Code,
        string Name,
        string? Description,
        int? ParentId,
        short DisplayOrder,
        bool IsActive);
}
