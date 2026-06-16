using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CancellationReasons.CreateCancellationReason;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.CancellationReasons;

internal sealed class CreateCancellationReasonEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cancellation-reasons",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CreateCancellationReason.Response> result = await sender.Send(new CreateCancellationReason.Command(
                        request.Code, request.Name, request.DisplayOrder, request.IsActive), ct);
                    return result.MatchCreated(u => $"/api/cancellation-reasons/{u.Id}");
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("CancellationReasons")
            .WithName("CreateCancellationReason")
            .Produces<ApiResult<CreateCancellationReason.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a cancellation reason.")
            .WithDescription(
                "Request: JSON body { code:string, name:string, displayOrder:short, isActive:bool }. Response: 201 Created — Location header; JSON body with new cancellation reason id.");
    }

    internal sealed record Request(string Code, string Name, short DisplayOrder, bool IsActive);
}
