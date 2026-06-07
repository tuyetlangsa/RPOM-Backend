using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Areas.UpdateArea;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Areas;

internal sealed class UpdateAreaEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/areas/{id:int}",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<UpdateArea.Response> result = await sender.Send(new UpdateArea.Command(
                        id, request.CounterId, request.Name, request.Description,
                        request.DisplayOrder, request.IsActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Areas")
            .WithName("UpdateArea")
            .Produces<ApiResult<UpdateArea.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update an area.")
            .WithDescription(
                "Request: route id (int); JSON body { counterId:int, name:string, description?:string, displayOrder:short, isActive:bool }. Response: 200 OK — JSON UpdateArea.Response.");
    }

    internal sealed record Request(
        int CounterId,
        string Name,
        string? Description,
        short DisplayOrder,
        bool IsActive);
}
