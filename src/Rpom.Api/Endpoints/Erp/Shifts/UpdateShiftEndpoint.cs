using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Shifts.UpdateShift;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Shifts;

internal sealed class UpdateShiftEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/shifts/{id:int}",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<UpdateShift.Response> result = await sender.Send(new UpdateShift.Command(
                        id, request.Code, request.Name,
                        TimeOnly.ParseExact(request.BeginTime, "HH:mm", null),
                        TimeOnly.ParseExact(request.EndTime, "HH:mm", null),
                        request.IsNextDay, request.Note, request.IsActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Shifts")
            .WithName("UpdateShift")
            .Produces<ApiResult<UpdateShift.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update a shift.")
            .WithDescription(
                "Request: route id (int); JSON body { code:string, name:string, beginTime:string(\"HH:mm\"), endTime:string(\"HH:mm\"), isNextDay:bool, note?:string, isActive:bool }. Response: 200 OK — JSON UpdateShift.Response.");
    }

    internal sealed record Request(
        string Code,
        string Name,
        string BeginTime,
        string EndTime,
        bool IsNextDay,
        string? Note,
        bool IsActive);
}
