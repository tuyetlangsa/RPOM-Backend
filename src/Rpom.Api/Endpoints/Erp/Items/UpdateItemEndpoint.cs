using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.UpdateItem;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class UpdateItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/items/{id:int}",
            async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var cats = request.Categories
                    .Select(c => new UpdateItem.CategoryInput(c.CategoryId, c.IsMain))
                    .ToList();
                var result = await sender.Send(new UpdateItem.Command(
                    id, request.Code, request.Name, request.Description, request.ImageUrl,
                    request.BaseUomId, request.VatPercent,
                    request.IsStockable, request.HasRecipe, request.LowStockThreshold,
                    request.KitchenStationId, request.IsActive, cats), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Items")
            .WithName("UpdateItem")
            .Produces<ApiResult<UpdateItem.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update an item.")
            .WithDescription("Request: route id (int); JSON body { code:string, name:string, description?:string, imageUrl?:string, baseUomId:int, vatPercent:decimal, isStockable:bool, hasRecipe:bool, lowStockThreshold?:decimal, kitchenStationId?:int, isActive:bool, categories:[{ categoryId:int, isMain:bool }] }. Response: 200 OK — JSON UpdateItem.Response.");
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
