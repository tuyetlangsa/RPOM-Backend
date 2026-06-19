using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Kitchen.GetKitchenAreas;

namespace Rpom.Api.Endpoints.Kitchen;

internal sealed class GetKitchenAreasEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/kitchen/areas",
                async (ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new GetKitchenAreas.Query(), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.KdsView)
            .Produces<ApiResult<GetKitchenAreas.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Kitchen")
            .WithName("GetKitchenAreas")
            .WithSummary("List of areas with dishes from the currently logged-in kitchen (to select an area → view menu → lock/unlock all items).");
    }
}
