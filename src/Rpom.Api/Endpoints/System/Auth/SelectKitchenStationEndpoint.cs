using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Access.SelectKitchenStation;

namespace Rpom.Api.Endpoints.System.Auth;

internal sealed class SelectKitchenStationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/auth/select-station",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new SelectKitchenStation.Command(request.KitchenStationId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.KdsView)
            .Produces<ApiResult<SelectKitchenStation.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Auth")
            .WithName("SelectKitchenStation")
            .WithSummary("Select the kitchen area for the session & reissue the JWT with the claim kitchen_station_id.");
    }

    internal sealed record Request(int KitchenStationId);
}
