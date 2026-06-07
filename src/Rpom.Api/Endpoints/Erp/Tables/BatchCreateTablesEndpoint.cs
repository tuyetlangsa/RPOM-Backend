using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Tables.BatchCreateTables;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Tables;

internal sealed class BatchCreateTablesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/tables/batch",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<BatchCreateTables.Response> result = await sender.Send(new BatchCreateTables.Command(
                        request.AreaId, request.CodePrefix, request.StartNumber, request.Count,
                        request.SeatCount, request.Description, request.IsActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Tables")
            .WithName("BatchCreateTables")
            .Produces<ApiResult<BatchCreateTables.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Batch-create tables in an area.")
            .WithDescription("""
    Request: JSON body { areaId:int, codePrefix:string, startNumber:int, count:int, seatCount:int,
    description?:string, isActive:bool }. Response: 200 OK — JSON BatchCreateTables.Response.
""");
    }

    internal sealed record Request(
        int AreaId,
        string CodePrefix,
        int StartNumber,
        int Count,
        int SeatCount,
        string? Description,
        bool IsActive);
}
