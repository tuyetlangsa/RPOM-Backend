using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.CreateItem;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class CreateItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/items",
            async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var cats = request.Categories
                    .Select(c => new CreateItem.CategoryInput(c.CategoryId, c.IsMain))
                    .ToList();
                var result = await sender.Send(new CreateItem.Command(
                    request.Code, request.Name, request.Description, request.ImageUrl,
                    request.BaseUomId, request.VatPercent,
                    request.IsStockable, request.HasRecipe, request.LowStockThreshold,
                    request.KitchenStationId, request.IsActive, cats), ct);
                return result.MatchCreated(i => $"/api/items/{i.Id}");
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Items")
            .WithName("CreateItem")
            .Produces<ApiResult<CreateItem.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create an item.")
            .WithDescription("Request: JSON body { code:string, name:string, description?:string, imageUrl?:string, baseUomId:int, vatPercent:decimal, isStockable:bool, hasRecipe:bool, lowStockThreshold?:decimal, kitchenStationId?:int, isActive:bool, categories:[{ categoryId:int, isMain:bool }] }. Response: 201 Created — Location header; JSON body with new item id.");
    }

    internal sealed record Request(
        string Code,
        string Name,
        string? Description,
        string? ImageUrl,
        int BaseUomId,
        decimal VatPercent,
        bool IsStockable,
        bool HasRecipe,
        decimal? LowStockThreshold,
        int? KitchenStationId,
        bool IsActive,
        IReadOnlyList<CategoryInput> Categories);

    internal sealed record CategoryInput(int CategoryId, bool IsMain);
}
