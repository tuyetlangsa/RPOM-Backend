using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Kitchen.GetIngredients;

namespace Rpom.Api.Endpoints.Kitchen;

internal sealed class GetIngredientsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/kitchen/ingredients",
                async ([FromQuery] long? orderItemId, [FromQuery] string? search, [FromQuery] bool? isActive,
                       ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new GetIngredients.Query(orderItemId, search, isActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.KdsView)
            .Produces<ApiResult<GetIngredients.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Kitchen")
            .WithName("GetKitchenIngredients")
            .WithSummary("Station stock: materials (via BOM) + stockable menu items, with qty + threshold. Pass orderItemId to split out that item's related stock.");
    }
}
