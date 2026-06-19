using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Kitchen.GetKitchenMenu;

namespace Rpom.Api.Endpoints.Kitchen;

internal sealed class GetKitchenMenuEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/kitchen/menu",
                async ([FromQuery] int areaId, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new GetKitchenMenu.Query(areaId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.KdsView)
            .Produces<ApiResult<GetKitchenMenu.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Kitchen")
            .WithName("GetKitchenMenu")
            .WithSummary("Kitchen menu by area: dishes belonging to that kitchen area + out-of-stock status in that area.");
    }
}
