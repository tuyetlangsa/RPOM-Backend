using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Areas.CreateArea;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Areas;

internal sealed class CreateAreaEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/areas",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CreateArea.Response> result = await sender.Send(new CreateArea.Command(
                        request.CounterId, request.Name, request.Description,
                        request.DisplayOrder, request.IsActive), ct);
                    return result.MatchCreated(a => $"/api/areas/{a.Id}");
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Areas")
            .WithName("CreateArea")
            .Produces<ApiResult<CreateArea.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create an area under a counter.")
            .WithDescription("""
    Request: JSON body { counterId:int, name:string, description?:string, displayOrder:short,
    isActive:bool }. Response: 201 Created — Location header; JSON body with new area id.
""");
    }

    internal sealed record Request(
        int CounterId,
        string Name,
        string? Description,
        short DisplayOrder,
        bool IsActive);
}
