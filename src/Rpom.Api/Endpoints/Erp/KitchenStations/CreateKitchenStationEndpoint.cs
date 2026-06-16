using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.KitchenStations.CreateKitchenStation;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.KitchenStations;

internal sealed class CreateKitchenStationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/kitchen-stations",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CreateKitchenStation.Response> result = await sender.Send(new CreateKitchenStation.Command(
                        request.Code, request.Name, request.Description, request.DisplayOrder, request.IsActive), ct);
                    return result.MatchCreated(u => $"/api/kitchen-stations/{u.Id}");
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("KitchenStations")
            .WithName("CreateKitchenStation")
            .Produces<ApiResult<CreateKitchenStation.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a kitchen station.")
            .WithDescription(
                "Request: JSON body { code:string, name:string, description?:string, displayOrder:short, isActive:bool }. Response: 201 Created — Location header; JSON body with new kitchen station id.");
    }

    internal sealed record Request(string Code, string Name, string? Description, short DisplayOrder, bool IsActive);
}
