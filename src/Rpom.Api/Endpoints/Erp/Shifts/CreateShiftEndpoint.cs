using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Shifts.CreateShift;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Shifts;

internal sealed class CreateShiftEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/shifts",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CreateShift.Response> result = await sender.Send(new CreateShift.Command(
                        request.Code, request.Name,
                        TimeOnly.ParseExact(request.BeginTime, "HH:mm", null),
                        TimeOnly.ParseExact(request.EndTime, "HH:mm", null),
                        request.IsNextDay, request.Note, request.IsActive), ct);
                    return result.MatchCreated(u => $"/api/shifts/{u.Id}");
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Shifts")
            .WithName("CreateShift")
            .Produces<ApiResult<CreateShift.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a shift.")
            .WithDescription(
                "Request: JSON body { code:string, name:string, beginTime:string(\"HH:mm\"), endTime:string(\"HH:mm\"), isNextDay:bool, note?:string, isActive:bool }. Response: 201 Created — Location header; JSON body with new shift id.");
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
