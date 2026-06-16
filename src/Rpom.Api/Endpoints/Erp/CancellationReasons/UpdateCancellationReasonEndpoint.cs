using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CancellationReasons.UpdateCancellationReason;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.CancellationReasons;

internal sealed class UpdateCancellationReasonEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/cancellation-reasons/{id:int}",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<UpdateCancellationReason.Response> result = await sender.Send(new UpdateCancellationReason.Command(
                        id, request.Code, request.Name, request.DisplayOrder, request.IsActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("CancellationReasons")
            .WithName("UpdateCancellationReason")
            .Produces<ApiResult<UpdateCancellationReason.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update a cancellation reason.")
            .WithDescription(
                "Request: route id (int); JSON body { code:string, name:string, displayOrder:short, isActive:bool }. Response: 200 OK — JSON UpdateCancellationReason.Response.");
    }

    internal sealed record Request(string Code, string Name, short DisplayOrder, bool IsActive);
}
