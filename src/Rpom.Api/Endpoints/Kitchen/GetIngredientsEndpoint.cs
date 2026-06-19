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
                async ([FromQuery] string? search, [FromQuery] bool? isActive,
                       ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new GetIngredients.Query(search, isActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.KdsView)
            .Produces<ApiResult<IReadOnlyList<GetIngredients.Ingredient>>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Kitchen")
            .WithName("GetKitchenIngredients")
            .WithSummary("Kitchen materials (via the BOM for each station item) + current inventory.");
    }
}
