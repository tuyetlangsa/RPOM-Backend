using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Uoms.UpdateUom;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Uoms;

internal sealed class UpdateUomEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/uoms/{id:int}",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<UpdateUom.Response> result = await sender.Send(new UpdateUom.Command(
                        id, request.Code, request.Name, request.Description, request.IsActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Uoms")
            .WithName("UpdateUom")
            .Produces<ApiResult<UpdateUom.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update a unit of measure.")
            .WithDescription(
                "Request: route id (int); JSON body { code:string, name:string, description?:string, isActive:bool }. Response: 200 OK — JSON UpdateUom.Response.");
    }

    internal sealed record Request(string Code, string Name, string? Description, bool IsActive);
}
