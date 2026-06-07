using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Tables.UpdateTable;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Tables;

internal sealed class UpdateTableEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/tables/{id:int}",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<UpdateTable.Response> result = await sender.Send(new UpdateTable.Command(
                        id, request.AreaId, request.Code, request.SeatCount,
                        request.Description, request.IsActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Tables")
            .WithName("UpdateTable")
            .Produces<ApiResult<UpdateTable.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update a table.")
            .WithDescription("""
    Request: route id (int); JSON body { areaId:int, code:string, seatCount:int, description?:string,
    isActive:bool }. Response: 200 OK — JSON UpdateTable.Response.
""");
    }

    internal sealed record Request(int AreaId, string Code, int SeatCount, string? Description, bool IsActive);
}
