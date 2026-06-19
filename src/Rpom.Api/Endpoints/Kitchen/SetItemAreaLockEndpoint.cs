using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Kitchen.SetItemAreaLock;

namespace Rpom.Api.Endpoints.Kitchen;

internal sealed class SetItemAreaLockEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/kitchen/items/{itemId:int}/lock",
                async (int itemId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new SetItemAreaLock.Command(
                            itemId, request.Lock, request.AreaIds,
                            request.AllServingAreas, request.Note), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ItemToggleAvailability)
            .Produces<ApiResult<SetItemAreaLock.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Kitchen")
            .WithName("SetItemAreaLock")
            .WithSummary("The kitchen staff will lock/unlock all items by area and notify the relevant counters. Gate off according to the kitchen area for each session.");
    }

    internal sealed record Request(
        bool Lock,
        IReadOnlyList<int>? AreaIds,
        bool AllServingAreas,
        string? Note);
}
