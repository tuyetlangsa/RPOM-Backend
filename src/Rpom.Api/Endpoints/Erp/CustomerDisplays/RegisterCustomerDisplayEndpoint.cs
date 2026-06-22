using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CustomerDisplays.RegisterCustomerDisplay;

namespace Rpom.Api.Endpoints.Erp.CustomerDisplays;

internal sealed class RegisterCustomerDisplayEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/customer-displays",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new RegisterCustomerDisplay.Command(request.PosTerminalId, request.Name, request.IdleMediaUrl), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .Produces<ApiResult<RegisterCustomerDisplay.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("CustomerDisplays")
            .WithName("RegisterCustomerDisplay")
            .WithSummary("Register customer screen connected to 1 POS machine (1:1) → return DeviceToken.");
    }

    internal sealed record Request(int PosTerminalId, string Name, string? IdleMediaUrl);
}
