using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.CustomerDisplays.ActivateCustomerDisplay;

namespace Rpom.Api.Endpoints.Guest.CustomerDisplay;

internal sealed class ActivateCustomerDisplayEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/customer-display/activate",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new ActivateCustomerDisplay.Command(request.DeviceToken, request.ClientId), ct);
                    return result.MatchOk();
                })
            .AllowAnonymous()
            .Produces<ApiResult<ActivateCustomerDisplay.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("CustomerDisplays")
            .WithName("ActivateCustomerDisplay")
            .WithSummary("Clients claim their first DeviceToken using ClientId — one token per device.");
    }

    internal sealed record Request(string DeviceToken, string ClientId);
}
