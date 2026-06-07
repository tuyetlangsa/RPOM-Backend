using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Counters.CreateCounter;

namespace Rpom.Api.Endpoints.Erp.Counters;

internal sealed class CreateCounterEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/counters",
            async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateCounter.Command(
                    request.Name, request.Note, request.DisplayOrder, request.IsActive), ct);
                return result.MatchCreated(c => $"/api/counters/{c.Id}");
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Counters")
            .WithName("CreateCounter")
            .Produces<ApiResult<CreateCounter.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a counter.")
            .WithDescription("Request: JSON body { name:string, note?:string, displayOrder:short, isActive:bool }. Response: 201 Created — Location header; JSON body with new counter id.");
    }

    internal sealed record Request(string Name, string? Note, short DisplayOrder, bool IsActive);
}
