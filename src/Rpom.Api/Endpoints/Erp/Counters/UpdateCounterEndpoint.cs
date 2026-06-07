using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Counters.UpdateCounter;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Counters;

internal sealed class UpdateCounterEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/counters/{id:int}",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<UpdateCounter.Response> result = await sender.Send(new UpdateCounter.Command(
                        id, request.Name, request.Note, request.DisplayOrder, request.IsActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Counters")
            .WithName("UpdateCounter")
            .Produces<ApiResult<UpdateCounter.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update a counter.")
            .WithDescription(
                "Request: route id (int); JSON body { name:string, note?:string, displayOrder:short, isActive:bool }. Response: 200 OK — JSON UpdateCounter.Response.");
    }

    internal sealed record Request(string Name, string? Note, short DisplayOrder, bool IsActive);
}
