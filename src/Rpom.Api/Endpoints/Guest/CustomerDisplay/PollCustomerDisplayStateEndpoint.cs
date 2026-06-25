using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.CustomerDisplays.PollCustomerDisplayState;

namespace Rpom.Api.Endpoints.Guest.CustomerDisplay;

internal sealed class PollCustomerDisplayStateEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/customer-display/poll",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new PollCustomerDisplayState.Command(request.DeviceToken, request.ClientId), ct);
                    return result.MatchOk();
                })
            .AllowAnonymous()
            .Produces<ApiResult<PollCustomerDisplayState.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("CustomerDisplays")
            .WithName("PollCustomerDisplayState")
            .WithSummary("The customer poll screen uses DeviceToken → IDLE (image/video) or QR code for customers to scan.");
    }

    internal sealed record Request(string DeviceToken, string ClientId);
}
