using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Uoms.CreateUom;

namespace Rpom.Api.Endpoints.Erp.Uoms;

internal sealed class CreateUomEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/uoms",
            async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateUom.Command(
                    request.Code, request.Name, request.Description, request.IsActive), ct);
                return result.MatchCreated(u => $"/api/uoms/{u.Id}");
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Uoms")
            .WithName("CreateUom")
            .Produces<ApiResult<CreateUom.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a unit of measure.")
            .WithDescription("Request: JSON body { code:string, name:string, description?:string, isActive:bool }. Response: 201 Created — Location header; JSON body with new uom id.");
    }

    internal sealed record Request(string Code, string Name, string? Description, bool IsActive);
}
